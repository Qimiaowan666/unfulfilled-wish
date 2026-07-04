using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFit : MonoBehaviour
{
    bool fitted;

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (!fitted)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one;
            float height = cam.orthographicSize * 2f;
            float width  = height * cam.aspect;
            Vector2 spriteSize = sr.sprite.bounds.size;
            transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
            fitted = true;
        }
        transform.position = new Vector3(cam.transform.position.x, transform.position.y, transform.position.z);
    }
}
