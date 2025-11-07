using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuScript : MonoBehaviour
{
    public bool controller = false;
    public bool paused = false;
    public int selectedIndex = 0;
    public List<GameObject> menuFull = new List<GameObject>();
    public List<GameObject> menuObjects = new List<GameObject>();
    public int count;
    public TextMeshProUGUI selectedText;
    public Color textColor;
    public Color highlightColor = Color.yellow;
    public float scaleMultiplier = 1.2f;
    public bool wentUp = false;
    public bool wentDown = false;
    // public void OnStartButton()
    // {
    //     Debug.Log("Start Button Pressed");
    //     UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    // }

    void Start()
    {
        count = menuObjects.Count;
    }

    void RefreshMenuObjects()
    {
        // Clear and repopulate the list
        menuObjects.Clear();

        // Find all TextMeshPro components in children
        GameObject[] buttonObjects = GameObject.FindGameObjectsWithTag("Button");
    
        // Get TextMeshPro components from those objects
        foreach (GameObject obj in buttonObjects)
        {
            menuObjects.Add(obj);
            // if (obj.layer == LayerMask.NameToLayer("Menu"))
            // {
            //     TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            //     if (text != null)
            //     {
            //         menuObjects.Add(text);
            //     }
            // }
        }
        count = menuObjects.Count;
        
        Debug.Log($"Found {count} TextMeshPro objects");
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        paused = true;
        foreach (GameObject obj in menuFull)
        {
            obj.SetActive(true);
        }
        RefreshMenuObjects();
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        paused = false;
        foreach (GameObject obj in menuFull)
        {
            obj.SetActive(false);
        }
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void Select()
    {
        if (selectedText.text == "Continue")
        {
            Resume();
        }
        else if (selectedText.text == "Quit")
        {
            Quit();
        }
    }

    public void SelectHover()
    {
        if (selectedText != null)
        {
            selectedText.color = textColor;
            selectedText.fontSize *= 1/scaleMultiplier;

        }
        selectedText = menuObjects[selectedIndex-1].GetComponent<TextMeshProUGUI>();
        textColor = selectedText.color;
        selectedText.color = highlightColor;
        selectedText.fontSize *= scaleMultiplier;
    }

    public void SelectUpOne()
    {
        if (wentUp) return;
        selectedIndex++;
        if (selectedIndex > menuObjects.Count)
        {
            selectedIndex = 1;
        }
        wentUp = true;
        SelectHover();
    }
    
    public void SelectDownOne()
    {
        if (wentDown) return;
        selectedIndex--;
        if (selectedIndex < 1)
        {
            selectedIndex = menuObjects.Count;
        }
        wentDown = true;
        SelectHover();
    }
}
