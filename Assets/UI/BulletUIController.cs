// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// public class BulletUIController : MonoBehaviour
// {
//     [Header("References")]
//     public RectTransform container;
//     public GameObject bulletPrefab;

//     [Header("Layout")]
//     public int totalBullets = 0;
//     public int bullets = 0;
//     public int rows = 2;
//     public Vector2 itemSize = new Vector2(32f, 32f);
//     public float padding = 4f;
//     public float itemScale = 1f;

//     private readonly List<Image> _items = new List<Image>();


//     public void SetUp() {
        
//     }

//     public void Trigger() {
//         SetCount(bullets);
//     }

//     void Start()
//     {
//         Rebuild();
//     }

// #if UNITY_EDITOR
//     void OnValidate()
//     {
//         if (!Application.isPlaying) return;
//         Rebuild();
//     }
// #endif

//     public void SetCount(int newCount)
//     {
//         count = newCount;
//         Rebuild();
//     }

//     public void Rebuild()
//     {
//         // Remove old items
//         foreach (var img in _items)
//         {
//             if (img != null) Destroy(img.gameObject);
//         }
//         _items.Clear();

//         if (count <= 0 || rows <= 0 || container == null) return;

//         int cols = Mathf.CeilToInt((float)count / rows);
//         Vector2 scaledSize = itemSize * itemScale;

//         for (int i = 0; i < count; i++)
//         {
//             int row = i / cols;
//             int col = i % cols;

//             var go = Instantiate(bulletPrefab);
//             go.transform.SetParent(container, false);

//             var rt = go.GetComponent<RectTransform>();
//             rt.anchorMin = Vector2.up;
//             rt.anchorMax = Vector2.up;
//             rt.pivot = Vector2.up;
//             rt.sizeDelta = scaledSize;

//             float x = col * (scaledSize.x + padding) + padding;
//             float y = -(row * (scaledSize.y + padding) + padding);
//             rt.anchoredPosition = new Vector2(x, y);

//             var img = go.GetComponent<Image>();
//             // img.sprite = itemSprite;
//             img.preserveAspect = true;

//             _items.Add(img);
//         }
//     }
// }
