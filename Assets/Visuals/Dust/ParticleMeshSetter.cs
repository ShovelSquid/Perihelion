using UnityEngine;

public class ParticleMeshSetter : MonoBehaviour
{
    public Transform particleTransform;
    public MeshRenderer mesh;
    public SkinnedMeshRenderer skinnedMesh;
    public ParticleSystem ps;
    public bool isMesh;

    void Awake()
    {
        // if (mesh == null) mesh = GetComponent<Renderer>();
        if (ps == null) ps = GetComponent<ParticleSystem>();
    }

    public void Activate(ParticleSystem system = null)
    {
        if ((mesh != null || skinnedMesh != null) && ps != null)
        {
            if (isMesh)
            {
                var m = new Mesh();
                if (mesh != null)
                {
                    ps.transform.localScale = mesh.transform.lossyScale;
                    m = mesh.GetComponent<MeshFilter>().sharedMesh;
                }
                else if (skinnedMesh != null)
                {
                    ps.transform.localScale = skinnedMesh.transform.lossyScale;
                    m = skinnedMesh.sharedMesh;
                }
                var shape = ps.shape;
                shape.mesh = m;
            }
            // ps.Stop(withChildren: true);
            // ps.Play(true);
            var p = Instantiate(system != null ? system : ps, particleTransform.position, particleTransform.rotation);
            p.gameObject.SetActive(true);
        }
    }
}
