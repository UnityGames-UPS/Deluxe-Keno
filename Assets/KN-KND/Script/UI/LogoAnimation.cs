using UnityEngine;
using System.Collections;
using UnityEngine.UI;
public class LogoAnimation : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite[] logoFrames;   // 28 sprites in correct order

    [Header("Animation Settings")]
    [SerializeField] private float frameRate = 0.05f;   // Time between frames
    [SerializeField] private float holdTime = 5f;        // Hold last frame in seconds

    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Start()
    {
        if (logoFrames.Length > 0)
        {
            StartCoroutine(PlayLogoAnimation());
        }
    }

    IEnumerator PlayLogoAnimation()
    {
        while (true)
        {
            // Play animation
            for (int i = 0; i < logoFrames.Length; i++)
            {
                image.sprite = logoFrames[i];
                yield return new WaitForSeconds(frameRate);
            }

            // Hold last frame
            yield return new WaitForSeconds(holdTime);
        }
    }
}
