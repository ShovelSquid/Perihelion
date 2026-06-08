using UnityEngine;
using System.Collections.Generic;

public class DamageStates : MonoBehaviour
{
    private Building building;
    public float healthPct;
    [System.Serializable]
    public struct DamageState
    {
        [Range(0f, 1f)]
        public float threshold;
        public Texture2D texture;
        public Inventory drop;
        public Mesh mesh;
        public ParticleSystem vfx;
        public DamageState(float threshold, Texture2D texture, Inventory drop, Mesh mesh, ParticleSystem vfx)
        {
            this.threshold = threshold;
            this.texture = texture;
            this.drop = drop;
            this.mesh = mesh;
            this.vfx = vfx;
        }
    }
    public List<DamageState> damageStates = new List<DamageState>();
    // public List<Texture2D> damageStates = new List<Texture2D>();
    public ParticleMeshSetter pms;
    public float currentDamageState = 0; // num correlating to num of list on building script for current texture
    // public List<Inventory> drops = new List<Inventory>();
    public Renderer mesh;
    private Material matInst;

    void Awake()
    {
        building = GetComponent<Building>();
        if (mesh == null) mesh = GetComponent<Renderer>();
        if (mesh != null && mesh.material != null)
        {
            matInst = new Material(mesh.material);
            mesh.material = matInst;
        }
    }

    public void UpdateDamageState(float healthPct)
    {
        if (damageStates.Count == 0 || mesh == null) return;

        // Thresholds descend (index 0 ~1, index n ~0). The active state is the
        // highest index whose threshold the health has dropped to/below; -1 means
        // health is above every threshold (undamaged).
        int target = -1;
        for (int i = 0; i < damageStates.Count; i++)
        {
            if (healthPct <= damageStates[i].threshold) target = i;
        }

        if (target != currentDamageState) SetDamageState(target);
        // if (state != currentDamageState)
        // {
        //     currentDamageState = state;
        //     SetDamageState((int)currentDamageState);
        //     // mesh.material.SetTexture("_BaseMap", damageStates[state].texture);
        //     // if (drops.Count > state && drops[state] != null)
        //     // {
        //     //     drops[state].Drop();
        //     // }
        //     // if (damageStates[state].vfx != null)
        //     // {
        //     //     damageStates[state].vfx.Activate();
        //     // }
        // }
    }

    public void SetDamageState(int statenum)
    {
        currentDamageState = statenum;
        Debug.Log("Applying damage state " + statenum);
        if (damageStates.Count == 0 || mesh == null) return;
        var i = statenum;
        if (statenum == -1)
        {
            // If above all thresholds, clear damage texture
            mesh.material.SetTexture("_BaseMap", null);
        }
        else
        {
            if (damageStates[i].texture != null) mesh.material.SetTexture("_BaseMap", damageStates[i].texture);
            if (damageStates[i].mesh != null) mesh.GetComponent<MeshFilter>().mesh = damageStates[i].mesh;
            if (damageStates[i].drop != null) damageStates[i].drop.Drop();
            if (pms != null && damageStates[i].vfx != null) pms.Activate(damageStates[i].vfx);
            else if (damageStates[i].vfx != null) Instantiate(damageStates[i].vfx, building.transform.position, Quaternion.identity);
            // else if (damageStates[i].vfx == null) pms.Activate();
        }
        // Drop items if any
        // if (drops.Count > i && drops[i] != null)
        // {
        //     drops[i].Drop();
        // }
        // do vfx if any
        // if (vfx != null)
        // {
        //     vfx.Activate();
        // }
    }

}
