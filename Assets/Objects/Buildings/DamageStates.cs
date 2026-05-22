using UnityEngine;
using System.Collections.Generic;

public class DamageStates : MonoBehaviour
{
    private Building building;
    public List<Texture2D> damageStates = new List<Texture2D>();
    public float currentDamageState = 0; // num correlating to num of list on building script for current texture
    public List<Inventory> drops = new List<Inventory>();
    public Renderer mesh;
    public ParticleMeshSetter vfx;
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

    public void UpdateDamageState(int state)
    {
        if (damageStates.Count == 0 || mesh == null) return;
        if (state != currentDamageState)
        {
            currentDamageState = state;
            // ApplyDamageState((int)currentDamageState);
            mesh.material.SetTexture("_BaseMap", damageStates[state]);
            if (drops.Count > state && drops[state] != null)
            {
                drops[state].Drop();
            }
            if (vfx != null)
            {
                vfx.Activate();
            }
        }
    }

    public void ApplyDamageState(int statenum)
    {
        Debug.Log("Applying damage state " + statenum);
        if (damageStates.Count == 0 || mesh == null) return;
        var i = statenum;
        if (statenum == -1)
        {
            // If above all thresholds, clear damage texture
            mesh.material.SetTexture("_DamageTex", null);
        }
        else
        {
            mesh.material.SetTexture("_DamageTex", damageStates[i]);
        }
        // Drop items if any
        if (drops.Count > i && drops[i] != null)
        {
            drops[i].Drop();
        }
        // do vfx if any
        if (vfx != null)
        {
            vfx.Activate();
        }
    }

}
