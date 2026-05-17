using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CuttableItem - Attach to any GameObject with a MeshFilter + MeshRenderer.
/// Right Arrow = horizontal cut (left to right, splits top/bottom halves)
/// Down Arrow  = vertical cut (top to bottom, splits left/right halves)
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CuttableItem : MonoBehaviour
{
    [Header("Cut Settings")]
    [Tooltip("How far the two halves fly apart after cutting")]
    public float separationForce = 3f;

    [Tooltip("Upward pop force applied to both halves")]
    public float popUpForce = 2f;

    [Tooltip("Torque applied to each half for a tumbling effect")]
    public float tumbleTorque = 200f;

    [Tooltip("Material to apply to the newly created cut face (cross-section). Leave null to reuse original.")]
    public Material cutFaceMaterial;

    [Tooltip("Optional particle effect spawned at the cut position")]
    public GameObject cutParticlePrefab;

    [Tooltip("Optional audio clip played on cut")]
    public AudioClip cutSound;

    // ---------------------------------------------------------------
    private bool _hasBeenCut = false;

    void Update()
    {
        if (_hasBeenCut) return;

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            // Horizontal plane through center → splits top / bottom
            Vector3 planeNormal = transform.up;          // world-up in local terms
            Vector3 planePoint  = transform.position;    // center of object
            Cut(planeNormal, planePoint, Vector3.right);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            // Vertical plane through center → splits left / right
            Vector3 planeNormal = transform.right;       // world-right in local terms
            Vector3 planePoint  = transform.position;
            Cut(planeNormal, planePoint, Vector3.up);
        }
    }

    // ---------------------------------------------------------------
    // Main cut routine
    // planeNormal  – the normal of the cutting plane (world space)
    // planePoint   – a point on the cutting plane (world space)
    // separationAxis – direction the two halves fly apart along
    // ---------------------------------------------------------------
    void Cut(Vector3 planeNormal, Vector3 planePoint, Vector3 separationAxis)
    {
        _hasBeenCut = true;

        MeshFilter mf = GetComponent<MeshFilter>();
        //Mesh originalMesh = mf.mesh;

        Mesh originalMesh = new Mesh();
        originalMesh.vertices = mf.mesh.vertices;
        originalMesh.normals = mf.mesh.normals;
        originalMesh.uv = mf.mesh.uv;
        originalMesh.triangles = mf.mesh.triangles;

        // Convert plane to local space
        Vector3 localNormal = transform.InverseTransformDirection(planeNormal).normalized;
        Vector3 localPoint  = transform.InverseTransformPoint(planePoint);

        // --- Split mesh data into two sides ---
        MeshData topData    = new MeshData();
        MeshData bottomData = new MeshData();

        Vector3[]  verts  = originalMesh.vertices;
        Vector3[]  norms  = originalMesh.normals;
        Vector2[]  uvs    = originalMesh.uv;
        int[]      tris   = originalMesh.triangles;

        // Collect edge intersection points for cap generation
        List<Vector3> capEdgePoints = new List<Vector3>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];

            Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
            Vector3 n0 = norms[i0], n1 = norms[i1], n2 = norms[i2];
            Vector2 u0 = uvs.Length > i0 ? uvs[i0] : Vector2.zero;
            Vector2 u1 = uvs.Length > i1 ? uvs[i1] : Vector2.zero;
            Vector2 u2 = uvs.Length > i2 ? uvs[i2] : Vector2.zero;

            float d0 = SignedDistance(v0, localPoint, localNormal);
            float d1 = SignedDistance(v1, localPoint, localNormal);
            float d2 = SignedDistance(v2, localPoint, localNormal);

            bool above0 = d0 >= 0, above1 = d1 >= 0, above2 = d2 >= 0;

            if (above0 && above1 && above2)
            {
                // Whole triangle is on the "top" side
                AddTriangle(topData, v0, v1, v2, n0, n1, n2, u0, u1, u2);
            }
            else if (!above0 && !above1 && !above2)
            {
                // Whole triangle is on the "bottom" side
                AddTriangle(bottomData, v0, v1, v2, n0, n1, n2, u0, u1, u2);
            }
            else
            {
                // Triangle straddles the plane – needs splitting
                SplitTriangle(
                    topData, bottomData,
                    v0, v1, v2,
                    n0, n1, n2,
                    u0, u1, u2,
                    d0, d1, d2,
                    localPoint, localNormal,
                    capEdgePoints
                );
            }
        }

        // Generate flat cap geometry from the intersection loop
        AddCap(topData,    capEdgePoints, localNormal);
        AddCap(bottomData, capEdgePoints, -localNormal);

        // Build Unity meshes
        Mesh topMesh    = BuildMesh(topData);
        Mesh bottomMesh = BuildMesh(bottomData);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        Material originalMat = mr.sharedMaterial;

        // Spawn top half
        GameObject topObj = CreateHalf("Top", topMesh, transform, originalMat, cutFaceMaterial);
        // Spawn bottom half
        GameObject botObj = CreateHalf("Bottom", bottomMesh, transform, originalMat, cutFaceMaterial);

        // Apply physics
        ApplyHalfPhysics(topObj,  separationAxis,  separationForce, popUpForce, tumbleTorque);
        ApplyHalfPhysics(botObj, -separationAxis,  separationForce, popUpForce, tumbleTorque);

        // Effects
        if (cutParticlePrefab != null)
            Instantiate(cutParticlePrefab, planePoint, Quaternion.identity);

        if (cutSound != null)
            AudioSource.PlayClipAtPoint(cutSound, transform.position);

        // Destroy original
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------
    // Geometry helpers
    // ---------------------------------------------------------------

    float SignedDistance(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        => Vector3.Dot(point - planePoint, planeNormal);

    Vector3 LinePlaneIntersect(Vector3 a, Vector3 b, Vector3 planePoint, Vector3 planeNormal)
    {
        float dA = Vector3.Dot(a - planePoint, planeNormal);
        float dB = Vector3.Dot(b - planePoint, planeNormal);
        float t  = dA / (dA - dB);
        return a + t * (b - a);
    }

    void SplitTriangle(
        MeshData top, MeshData bot,
        Vector3 v0, Vector3 v1, Vector3 v2,
        Vector3 n0, Vector3 n1, Vector3 n2,
        Vector2 u0, Vector2 u1, Vector2 u2,
        float d0, float d1, float d2,
        Vector3 planePoint, Vector3 planeNormal,
        List<Vector3> capPoints)
    {
        // Pack into arrays for easier handling
        Vector3[] v = { v0, v1, v2 };
        Vector3[] n = { n0, n1, n2 };
        Vector2[] u = { u0, u1, u2 };
        float[]   d = { d0, d1, d2 };
        bool[]    above = { d0 >= 0, d1 >= 0, d2 >= 0 };

        // Count vertices on each side
        List<int> aboveIdx = new List<int>();
        List<int> belowIdx = new List<int>();
        for (int i = 0; i < 3; i++)
            (above[i] ? aboveIdx : belowIdx).Add(i);

        // Two intersection points exist for any straddling triangle
        // Find the edges that cross the plane
        List<(int a, int b)> crossEdges = new List<(int, int)>();
        for (int i = 0; i < 3; i++)
        {
            int j = (i + 1) % 3;
            if (above[i] != above[j])
                crossEdges.Add((i, j));
        }

        // Interpolate intersection vertices
        Vector3[] iVerts = new Vector3[2];
        Vector3[] iNorms = new Vector3[2];
        Vector2[] iUvs   = new Vector2[2];

        for (int k = 0; k < 2; k++)
        {
            int a = crossEdges[k].a, b = crossEdges[k].b;
            float t = Mathf.Abs(d[a]) / (Mathf.Abs(d[a]) + Mathf.Abs(d[b]));
            iVerts[k] = Vector3.Lerp(v[a], v[b], t);
            iNorms[k] = Vector3.Lerp(n[a], n[b], t).normalized;
            iUvs[k]   = Vector2.Lerp(u[a], u[b], t);
            capPoints.Add(iVerts[k]);
        }

        // Distribute sub-triangles to correct sides
        if (aboveIdx.Count == 1)
        {
            // One vertex above: 1 triangle on top, 2 on bottom
            int ai = aboveIdx[0];
            AddTriangle(top, v[ai], iVerts[0], iVerts[1], n[ai], iNorms[0], iNorms[1], u[ai], iUvs[0], iUvs[1]);

            // Two triangles fill the bottom quad
            int b0 = belowIdx[0], b1 = belowIdx[1];
            AddTriangle(bot, v[b0], v[b1], iVerts[1], n[b0], n[b1], iNorms[1], u[b0], u[b1], iUvs[1]);
            AddTriangle(bot, v[b0], iVerts[1], iVerts[0], n[b0], iNorms[1], iNorms[0], u[b0], iUvs[1], iUvs[0]);
        }
        else
        {
            // Two vertices above: 2 triangles on top, 1 on bottom
            int bi = belowIdx[0];
            AddTriangle(bot, v[bi], iVerts[0], iVerts[1], n[bi], iNorms[0], iNorms[1], u[bi], iUvs[0], iUvs[1]);

            int a0 = aboveIdx[0], a1 = aboveIdx[1];
            AddTriangle(top, v[a0], v[a1], iVerts[1], n[a0], n[a1], iNorms[1], u[a0], u[a1], iUvs[1]);
            AddTriangle(top, v[a0], iVerts[1], iVerts[0], n[a0], iNorms[1], iNorms[0], u[a0], iUvs[1], iUvs[0]);
        }
    }

    // ---------------------------------------------------------------
    // Build a flat cap polygon from the ring of intersection points
    // using a simple fan triangulation around the centroid.
    // ---------------------------------------------------------------
    void AddCap(MeshData data, List<Vector3> edgePoints, Vector3 capNormal)
    {
        if (edgePoints.Count < 3) return;

        // Compute centroid
        Vector3 centroid = Vector3.zero;
        foreach (var p in edgePoints) centroid += p;
        centroid /= edgePoints.Count;

        // Sort points around the centroid to form a convex polygon
        Vector3 tangent = Vector3.Cross(capNormal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(capNormal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(capNormal, tangent).normalized;

        edgePoints.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(Vector3.Dot(a - centroid, bitangent), Vector3.Dot(a - centroid, tangent));
            float angleB = Mathf.Atan2(Vector3.Dot(b - centroid, bitangent), Vector3.Dot(b - centroid, tangent));
            return angleA.CompareTo(angleB);
        });

        // Fan triangulate
        for (int i = 0; i < edgePoints.Count; i++)
        {
            int next = (i + 1) % edgePoints.Count;
            Vector2 uvA = new Vector2(Vector3.Dot(edgePoints[i]    - centroid, tangent), Vector3.Dot(edgePoints[i]    - centroid, bitangent));
            Vector2 uvB = new Vector2(Vector3.Dot(edgePoints[next] - centroid, tangent), Vector3.Dot(edgePoints[next] - centroid, bitangent));

            // Use submesh index 1 for the cap face
            AddTriangleToSubmesh(data, 1,
                centroid,       edgePoints[i],     edgePoints[next],
                capNormal,      capNormal,          capNormal,
                Vector2.zero,   uvA,                uvB);
        }
    }

    // ---------------------------------------------------------------
    // MeshData helpers
    // ---------------------------------------------------------------

    void AddTriangle(MeshData d,
        Vector3 v0, Vector3 v1, Vector3 v2,
        Vector3 n0, Vector3 n1, Vector3 n2,
        Vector2 u0, Vector2 u1, Vector2 u2)
    {
        AddTriangleToSubmesh(d, 0, v0, v1, v2, n0, n1, n2, u0, u1, u2);
    }

    void AddTriangleToSubmesh(MeshData d, int submesh,
        Vector3 v0, Vector3 v1, Vector3 v2,
        Vector3 n0, Vector3 n1, Vector3 n2,
        Vector2 u0, Vector2 u1, Vector2 u2)
    {
        while (d.submeshTris.Count <= submesh)
            d.submeshTris.Add(new List<int>());

        int baseIdx = d.vertices.Count;
        d.vertices.Add(v0); d.vertices.Add(v1); d.vertices.Add(v2);
        d.normals.Add(n0);  d.normals.Add(n1);  d.normals.Add(n2);
        d.uvs.Add(u0);      d.uvs.Add(u1);      d.uvs.Add(u2);

        d.submeshTris[submesh].Add(baseIdx);
        d.submeshTris[submesh].Add(baseIdx + 1);
        d.submeshTris[submesh].Add(baseIdx + 2);
    }

    Mesh BuildMesh(MeshData d)
    {
        Mesh m = new Mesh();
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(d.vertices);
        m.SetNormals(d.normals);
        m.SetUVs(0, d.uvs);

        m.subMeshCount = d.submeshTris.Count;
        for (int s = 0; s < d.submeshTris.Count; s++)
            m.SetTriangles(d.submeshTris[s], s);

        m.RecalculateBounds();
        return m;
    }

    // ---------------------------------------------------------------
    // Creates a new GameObject for a cut half
    // ---------------------------------------------------------------
    GameObject CreateHalf(string name, Mesh mesh, Transform source, Material originalMat, Material capMat)
    {
        GameObject obj = new GameObject(name + "_Half");
        obj.transform.position   = source.position;
        obj.transform.rotation   = source.rotation;
        obj.transform.localScale = source.localScale;

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        // Sub-mesh 0 = original material, sub-mesh 1 = cap material
        Material cap = capMat != null ? capMat : originalMat;
        mr.sharedMaterials = mesh.subMeshCount > 1
            ? new Material[] { originalMat, cap }
            : new Material[] { originalMat };

        // Add a collider that matches the new shape
        MeshCollider mc = obj.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex     = true;

        return obj;
    }

    // ---------------------------------------------------------------
    // Apply rigidbody forces so the halves fly apart realistically
    // ---------------------------------------------------------------
    void ApplyHalfPhysics(GameObject obj, Vector3 direction, float force, float upForce, float torque)
    {
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = true;

        Vector3 worldDir = transform.TransformDirection(direction).normalized;
        rb.AddForce(worldDir * force + Vector3.up * upForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torque);

        // Auto-destroy after a few seconds to keep the scene clean
        Destroy(obj, 4f);
    }

    // ---------------------------------------------------------------
    // Simple mesh data container
    // ---------------------------------------------------------------
    class MeshData
    {
        public List<Vector3>      vertices    = new List<Vector3>();
        public List<Vector3>      normals     = new List<Vector3>();
        public List<Vector2>      uvs         = new List<Vector2>();
        public List<List<int>>    submeshTris = new List<List<int>>();
    }
}
