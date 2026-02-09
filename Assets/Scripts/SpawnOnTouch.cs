using UnityEngine;

public class SpawnOnTouch : MonoBehaviour
{
    public GameObject prefab; 
    public Camera mainCamera; 

    [SerializeField] private float worldSpawnZ = 0f;
    [SerializeField] private float destroyAfterSeconds = 0f;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) Spawn(Input.mousePosition);

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) Spawn(touch.position);
        }
    }

    private void Spawn(Vector3 screenPosition)
    {
        if (prefab == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        RectTransform parentRect = transform as RectTransform;
        RectTransform prefabRect = prefab.transform as RectTransform;

        if (parentRect != null && prefabRect != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCam = canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, eventCam, out Vector2 localPoint))
            {
                GameObject go = Instantiate(prefab, parentRect);
                RectTransform rt = go.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition = localPoint;
                }

                ScheduleAutoDestroyIfParticle(go);
            }
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, worldSpawnZ));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPosition = ray.GetPoint(enter);
            GameObject go = Instantiate(prefab, worldPosition, Quaternion.identity, this.transform);
            ScheduleAutoDestroyIfParticle(go);
        }
    }

    private void ScheduleAutoDestroyIfParticle(GameObject go)
    {
        if (go == null) return;

        float manual = destroyAfterSeconds;
        if (manual > 0f)
        {
            Destroy(go, manual);
            return;
        }

        ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0) return;

        float maxLifetime = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null) continue;

            var main = ps.main;
            if (main.loop) continue;

            float delay = main.startDelay.constantMax;
            float dur = main.duration;
            float life = main.startLifetime.constantMax;
            float total = delay + dur + life;
            if (total > maxLifetime) maxLifetime = total;
        }

        if (maxLifetime <= 0f) return;
        Destroy(go, maxLifetime);
    }
}
