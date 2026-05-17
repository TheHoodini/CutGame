using System.Collections.Generic;
using UnityEngine;

public class CuttableObject : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Cut Settings")]
    [Tooltip("World-space cut angle (degrees, clockwise from horizontal).\n" +
             "0 = horizontal (left↔right), 90 = vertical (top↕bottom),\n" +
             "45 = diagonal upper-left→lower-right, 135 = upper-right→lower-left.")]
    [Range(0f, 360f)]
    [SerializeField] public float cutAngle = 0f;

    [Header("Cut Cap")]
    [Tooltip("Texture applied to the cut face. Leave blank for white.")]
    [SerializeField] private Texture2D capTexture;
    [Tooltip("Optional full material for the cut face. Overrides capTexture.")]
    [SerializeField] private Material capMaterialOverride;

    [Header("Physics")]
    [SerializeField] private bool addRigidbodyOnCut = true;
    [SerializeField] private float separationForce = 2f;

    private bool _isCutPiece; // block cut if already cut

    // ─────────────────────────────────────────────────────────────────────────

    /*
    private void Update()
    {
        if (_isCutPiece) return;
        if (Input.GetKeyDown(KeyCode.Space))
            CutAtAngle(cutAngle);
    }
    */

    // ─── Public Cut ───────────────────────────────────────────────────────────

    /// Trigger a cut at the given world-space angle (degrees, clockwise from horizontal).
    /// The plane always passes through the combined world-space centroid of all
    /// child MeshRenderers, so the cut bisects the visual centre of the model.
    public void CutAtAngle(float angleDeg)
    {
        if (_isCutPiece) return;
        // ── Angle → plane normal ──────────────────────────────────────────────
        // 0° is horizontal: normal = world up = (0, 1, 0).
        //   normal = ( sin(rad), cos(rad), 0 )
        //   0°   → (0,  1, 0) → horizontal cut
        //   90°  → (1,  0, 0) → vertical cut
        //   45°  → (0.707, 0.707, 0) → diagonal
        //   135° → (0.707, -0.707, 0) → opposite diagonal
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 planeNormal = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
        Vector3 capUVNormal = new Vector3(Mathf.Cos(rad), -Mathf.Sin(rad), 0f);

        // ── Plane origin: combined centroid of all child renderers ────────────
        // Using transform.position would anchor the plane to the pivot, which
        // may not be the visual centre of the model (common with FBX imports).
        // Instead we compute the world-space centre of all MeshRenderer bounds.
        Vector3 centroid = ComputeHierarchyCentroid();

        Plane worldPlane = new Plane(planeNormal, centroid);
        Cut(worldPlane, capUVNormal);
    }

    /// Returns the world-space centre of the combined AABB of every MeshRenderer
    /// in the hierarchy. Falls back to transform.position if none are found.
    private Vector3 ComputeHierarchyCentroid()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        if (renderers.Length == 0) return transform.position;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return combined.center;
    }

    /// Perform a cut with an explicit world-space plane.
    public void Cut(Plane worldPlane, Vector3 capUVNormal)
    {
        // Collect every MeshFilter in this hierarchy (children + self)
        MeshFilter[] allFilters = GetComponentsInChildren<MeshFilter>(includeInactive: true);
        if (allFilters.Length == 0)
        {
            Debug.LogWarning("[CuttableItem2] No MeshFilters found in hierarchy.");
            return;
        }

        Material capMat = GetOrCreateCapMaterial();

        // Create the two result root GameObjects, positioned/rotated like the original root.
        // Scale is intentionally set to (1,1,1) here because every child will carry its own
        // world-derived localScale — applying root scale again would double-scale them.
        GameObject rootA = CreateResultRoot("Cut_A");
        GameObject rootB = CreateResultRoot("Cut_B");

        bool anyA = false, anyB = false;

        foreach (MeshFilter mf in allFilters)
        {
            Mesh srcMesh = mf.sharedMesh;
            if (srcMesh == null) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            // Slice this child's mesh
            MeshData above = new MeshData();
            MeshData below = new MeshData();
            List<Vector3> cutEdge = new List<Vector3>();

            SliceMesh(srcMesh, mf.transform, worldPlane, above, below, cutEdge);

            bool hasAbove = above.bodyTriangles.Count > 0;
            bool hasBelow = below.bodyTriangles.Count > 0;

            if (!hasAbove && !hasBelow) continue; // mesh fully outside frustum (degenerate)

            if (hasAbove && hasBelow)
            {
                // Mesh straddles the plane — build caps and add to both sides
                List<Vector3> loop = OrderEdgeLoop(cutEdge);
                AddCap(above, loop, worldPlane.normal, capUVNormal, mf.transform);
                AddCap(below, loop, -worldPlane.normal, capUVNormal, mf.transform);

                AttachSlicedChild(rootA, mf, mr, above.Build(), capMat);
                AttachSlicedChild(rootB, mf, mr, below.Build(), capMat);
                anyA = anyB = true;
            }
            else if (hasAbove)
            {
                // Entirely on the "above" side — duplicate intact (no re-mesh needed)
                AttachIntactChild(rootA, mf, mr);
                anyA = true;
            }
            else
            {
                // Entirely on the "below" side
                AttachIntactChild(rootB, mf, mr);
                anyB = true;
            }
        }

        // Discard any result root that ended up with no geometry
        if (!anyA) Destroy(rootA);
        if (!anyB) Destroy(rootB);

        // Add physics separation after all children are attached
        if (addRigidbodyOnCut)
        {
            if (anyA) ApplySeparation(rootA, worldPlane.normal, separationForce);
            if (anyB) ApplySeparation(rootB, -worldPlane.normal, separationForce);
        }

        // Mark spawned pieces so they don't re-trigger on their own
        if (anyA) SetupResultPiece(rootA);
        if (anyB) SetupResultPiece(rootB);

        Destroy(gameObject);
    }

    // ─── Result root helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates an empty root GameObject at the same world position/rotation as this object.
    /// Scale is (1,1,1) — children carry their own correct world-derived scale.
    /// </summary>
    private GameObject CreateResultRoot(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = Vector3.one; // children handle their own scale
        return go;
    }

    /// <summary>
    /// Attaches a NEW child to resultRoot that carries the sliced mesh.
    /// The child's world transform is copied exactly from the source MeshFilter's transform,
    /// preserving position, rotation, and scale regardless of nesting depth.
    /// </summary>
    private static void AttachSlicedChild(GameObject resultRoot,
                                          MeshFilter sourceMF,
                                          MeshRenderer sourceMR,
                                          Mesh slicedMesh,
                                          Material capMat)
    {
        GameObject child = new GameObject(sourceMF.name + "_slice");

        // ── Preserve the exact world transform of the original child ──────────
        // Setting parent AFTER assigning world position/rotation/scale avoids
        // Unity re-computing localScale through the new parent's scale chain.
        child.transform.SetPositionAndRotation(
            sourceMF.transform.position,
            sourceMF.transform.rotation);

        // Compute the localScale the child needs so it appears at the correct
        // world scale under the new root (which has scale 1,1,1).
        // Because resultRoot.scale = (1,1,1), localScale == lossyScale here,
        // but we use the explicit formula to be safe with any root scale.
        child.transform.SetParent(resultRoot.transform, worldPositionStays: true);
        child.transform.localScale = sourceMF.transform.lossyScale;

        // ── Mesh ──────────────────────────────────────────────────────────────
        child.AddComponent<MeshFilter>().mesh = slicedMesh;

        // ── Materials: original slots + cap as last slot ───────────────────────
        MeshRenderer mr = child.AddComponent<MeshRenderer>();
        Material[] origMats = sourceMR.sharedMaterials;
        Material[] allMats = new Material[origMats.Length + 1];
        origMats.CopyTo(allMats, 0);
        allMats[allMats.Length - 1] = capMat;
        mr.sharedMaterials = allMats;

        // ── Collider ──────────────────────────────────────────────────────────
        AddSafeCollider(child, slicedMesh, child.name);
    }

    /// <summary>
    /// Attaches a child that was entirely on one side — no re-meshing needed.
    /// Instantiates a copy of the source GameObject (preserving its mesh + materials)
    /// and re-parents it under resultRoot with the correct world transform.
    /// </summary>
    private static void AttachIntactChild(GameObject resultRoot,
                                          MeshFilter sourceMF,
                                          MeshRenderer sourceMR)
    {
        GameObject child = new GameObject(sourceMF.name + "_intact");

        child.transform.SetPositionAndRotation(
            sourceMF.transform.position,
            sourceMF.transform.rotation);

        child.transform.SetParent(resultRoot.transform, worldPositionStays: true);
        child.transform.localScale = sourceMF.transform.lossyScale;

        // Re-use the original shared mesh (no cut was applied)
        child.AddComponent<MeshFilter>().mesh = sourceMF.sharedMesh;

        MeshRenderer mr = child.AddComponent<MeshRenderer>();
        mr.sharedMaterials = sourceMR.sharedMaterials;

        AddSafeCollider(child, sourceMF.sharedMesh, child.name);
    }

    /// <summary>
    /// Adds a MeshCollider, trying convex first (needed for Rigidbody).
    /// Falls back to non-convex if PhysX rejects it (>255 verts etc.).
    /// </summary>
    private static void AddSafeCollider(GameObject go, Mesh mesh, string label)
    {
        MeshCollider col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        try { col.convex = true; }
        catch
        {
            col.convex = false;
            Debug.LogWarning($"[CuttableItem2] '{label}' too complex for convex collider — " +
                              "using non-convex (static only).");
        }
    }

    private void ApplySeparation(GameObject root, Vector3 direction, float force)
    {
        // Collect all colliders; if any child is non-convex we cannot add a Rigidbody
        // to that child's GameObject, but we CAN add one to the root (which has no collider).
        // The root Rigidbody will move all children together.
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    /// <summary>
    /// Tags the result root so its CuttableItem2 component won't listen to input,
    /// but can still be cut again programmatically via CutAtAngle() / Cut().
    /// </summary>
    private void SetupResultPiece(GameObject root)
    {
        CuttableObject ci = root.AddComponent<CuttableObject>();
        ci._isCutPiece = true;
        ci.cutAngle = cutAngle;
        ci.capTexture = capTexture;
        ci.capMaterialOverride = capMaterialOverride;
        ci.addRigidbodyOnCut = addRigidbodyOnCut;
        ci.separationForce = separationForce;
    }

    // ─── Mesh slicing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Slices <paramref name="mesh"/> against <paramref name="worldPlane"/>.
    /// <paramref name="childTransform"/> is the MeshFilter's own transform, used
    /// to convert local vertex positions to world space for plane testing, then
    /// back to local space for the output mesh.
    /// </summary>
    private static void SliceMesh(Mesh mesh,
                                   Transform childTransform,
                                   Plane worldPlane,
                                   MeshData above,
                                   MeshData below,
                                   List<Vector3> cutEdge)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] tris = mesh.triangles;

        // Classify vertices in world space
        bool[] aboveSide = new bool[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            aboveSide[i] = worldPlane.GetSide(childTransform.TransformPoint(verts[i]));

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            bool a0 = aboveSide[i0], a1 = aboveSide[i1], a2 = aboveSide[i2];

            if (a0 && a1 && a2)
                AddBodyTriangle(above, verts, norms, uvs, i0, i1, i2);
            else if (!a0 && !a1 && !a2)
                AddBodyTriangle(below, verts, norms, uvs, i0, i1, i2);
            else
                ClipTriangle(verts, norms, uvs, i0, i1, i2, a0, a1, a2,
                             worldPlane, childTransform, above, below, cutEdge);
        }
    }

    private static void ClipTriangle(Vector3[] verts,
                                      Vector3[] norms,
                                      Vector2[] uvs,
                                      int i0, int i1, int i2,
                                      bool a0, bool a1, bool a2,
                                      Plane worldPlane,
                                      Transform childTransform,
                                      MeshData above,
                                      MeshData below,
                                      List<Vector3> cutEdge)
    {
        var pts = new (int idx, bool above)[] { (i0, a0), (i1, a1), (i2, a2) };

        var abovePts = new List<VertexData>();
        var belowPts = new List<VertexData>();

        for (int i = 0; i < 3; i++)
        {
            var cur = pts[i];
            var next = pts[(i + 1) % 3];

            var vd = MakeVertex(verts, norms, uvs, cur.idx);
            if (cur.above) abovePts.Add(vd); else belowPts.Add(vd);

            if (cur.above != next.above)
            {
                // Intersect in local space using the plane expressed in local space
                VertexData iv = IntersectEdge(verts, norms, uvs,
                                              cur.idx, next.idx,
                                              worldPlane, childTransform);
                abovePts.Add(iv);
                belowPts.Add(iv);
                // Record the world-space intersection point for cap generation
                cutEdge.Add(childTransform.TransformPoint(iv.position));
            }
        }

        FanTriangulateBody(abovePts, above);
        FanTriangulateBody(belowPts, below);
    }

    // ─── Cap generation ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a filled cap polygon from the ordered world-space edge loop.
    /// Vertices are converted back to the child's local space for the output mesh.
    /// </summary>
    private static void AddCap(MeshData data,
                                List<Vector3> orderedWorldLoop,
                                Vector3 faceNormal,
                                Vector3 capNormal,
                                Transform childTransform)
    {
        if (orderedWorldLoop.Count < 3) return;

        Vector3 right = Vector3.Cross(faceNormal, capNormal).normalized;
        Vector3 up = Vector3.Cross(right, faceNormal).normalized;

        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        var localPositions = new List<Vector3>(orderedWorldLoop.Count);
        foreach (var wp in orderedWorldLoop)
        {
            // Convert world intersection point back to this child's local space
            localPositions.Add(childTransform.InverseTransformPoint(wp));
            float u = Vector3.Dot(wp, right);
            float v = Vector3.Dot(wp, up);
            if (u < minU) minU = u; if (u > maxU) maxU = u;
            if (v < minV) minV = v; if (v > maxV) maxV = v;
        }

        float uRange = Mathf.Max(maxU - minU, 1e-5f);
        float vRange = Mathf.Max(maxV - minV, 1e-5f);
        Vector3 localNrm = childTransform.InverseTransformDirection(faceNormal).normalized;

        var capVerts = new List<VertexData>(orderedWorldLoop.Count);
        for (int i = 0; i < localPositions.Count; i++)
        {
            Vector3 wp = orderedWorldLoop[i];
            capVerts.Add(new VertexData
            {
                position = localPositions[i],
                normal = localNrm,
                uv = new Vector2(
                               (Vector3.Dot(wp, right) - minU) / uRange,
                               (Vector3.Dot(wp, up) - minV) / vRange)
            });
        }

        FanTriangulateCap(capVerts, data, localNrm);
    }

    // ─── Edge loop ordering ───────────────────────────────────────────────────

    private static List<Vector3> OrderEdgeLoop(List<Vector3> raw)
    {
        if (raw.Count == 0) return raw;

        var remaining = new List<Vector3>(raw);
        var ordered = new List<Vector3> { remaining[0] };
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            Vector3 last = ordered[ordered.Count - 1];
            int nearest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                float d = Vector3.SqrMagnitude(remaining[i] - last);
                if (d < minDist) { minDist = d; nearest = i; }
            }

            ordered.Add(remaining[nearest]);
            remaining.RemoveAt(nearest);
        }

        return ordered;
    }

    // ─── Material helpers ─────────────────────────────────────────────────────

    private Material GetOrCreateCapMaterial()
    {
        if (capMaterialOverride != null) return capMaterialOverride;

        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = capTexture; // null is fine — renders white
        if (capTexture == null) mat.color = Color.white;
        return mat;
    }

    // ─── Low-level vertex / triangle helpers ──────────────────────────────────

    private static VertexData MakeVertex(Vector3[] v, Vector3[] n, Vector2[] uv, int idx)
    {
        return new VertexData
        {
            position = v[idx],
            normal = (n != null && idx < n.Length) ? n[idx] : Vector3.up,
            uv = (uv != null && idx < uv.Length) ? uv[idx] : Vector2.zero
        };
    }

    /// <summary>
    /// Intersects local-space edge [ia→ib] with the world plane.
    /// Converts both endpoints to world space for distance testing,
    /// but interpolates in local space so the output vertex stays local.
    /// </summary>
    private static VertexData IntersectEdge(Vector3[] verts,
                                             Vector3[] norms,
                                             Vector2[] uvs,
                                             int ia,
                                             int ib,
                                             Plane worldPlane,
                                             Transform childTransform)
    {
        // World-space positions for plane distance test
        Vector3 wa = childTransform.TransformPoint(verts[ia]);
        Vector3 wb = childTransform.TransformPoint(verts[ib]);

        float da = worldPlane.GetDistanceToPoint(wa);
        float db = worldPlane.GetDistanceToPoint(wb);
        float denom = da - db;
        float t = Mathf.Abs(denom) < 1e-6f ? 0.5f : da / denom;

        return new VertexData
        {
            // Interpolate in local space so the mesh stays in local coords
            position = Vector3.Lerp(verts[ia], verts[ib], t),
            normal = Vector3.Lerp(
                           (norms != null && ia < norms.Length) ? norms[ia] : Vector3.up,
                           (norms != null && ib < norms.Length) ? norms[ib] : Vector3.up, t).normalized,
            uv = Vector2.Lerp(
                           (uvs != null && ia < uvs.Length) ? uvs[ia] : Vector2.zero,
                           (uvs != null && ib < uvs.Length) ? uvs[ib] : Vector2.zero, t)
        };
    }

    private static void AddBodyTriangle(MeshData data,
                                        Vector3[] verts, Vector3[] norms, Vector2[] uvs,
                                        int i0, int i1, int i2)
    {
        FanTriangulateBody(new List<VertexData>
        {
            MakeVertex(verts, norms, uvs, i0),
            MakeVertex(verts, norms, uvs, i1),
            MakeVertex(verts, norms, uvs, i2)
        }, data);
    }

    private static void FanTriangulateBody(List<VertexData> pts, MeshData data)
    {
        if (pts.Count < 3) return;
        int base0 = data.AddVertex(pts[0]);
        for (int i = 1; i < pts.Count - 1; i++)
        {
            data.bodyTriangles.Add(base0);
            data.bodyTriangles.Add(data.AddVertex(pts[i]));
            data.bodyTriangles.Add(data.AddVertex(pts[i + 1]));
        }
    }

    private static void FanTriangulateCap(List<VertexData> pts, MeshData data, Vector3 normal)
    {
        if (pts.Count < 3) return;
        int base0 = data.AddVertex(pts[0], normal);
        for (int i = 1; i < pts.Count - 1; i++)
        {
            data.capTriangles.Add(base0);
            data.capTriangles.Add(data.AddVertex(pts[i], normal));
            data.capTriangles.Add(data.AddVertex(pts[i + 1], normal));
        }
    }

    // ─── Inner types ──────────────────────────────────────────────────────────

    private struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
    }

    /// <summary>
    /// Vertex buffer + two triangle lists for two independent sub-meshes:
    ///   bodyTriangles → sub-mesh 0  (original surface material)
    ///   capTriangles  → sub-mesh 1  (cap / patch material)
    /// </summary>
    private class MeshData
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> bodyTriangles = new List<int>();
        public readonly List<int> capTriangles = new List<int>();

        public int AddVertex(VertexData vd, Vector3? overrideNormal = null)
        {
            int idx = vertices.Count;
            vertices.Add(vd.position);
            normals.Add(overrideNormal ?? vd.normal);
            uvs.Add(vd.uv);
            return idx;
        }

        public Mesh Build()
        {
            Mesh m = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                subMeshCount = 2
            };
            m.SetVertices(vertices);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(bodyTriangles, 0);
            m.SetTriangles(capTriangles, 1);
            m.RecalculateBounds();
            return m;
        }
    }
}