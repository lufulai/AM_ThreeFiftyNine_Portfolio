using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class JokboScrollClickTarget : InteractiveObjectBase
{
    private const string PresentationLightName = "Jokbo Scroll Light";
    private const uint PresentationLightRenderingLayerMask = 1u << 7;

    [SerializeField] private JokboCheckUI jokboCheckUI;
    [SerializeField] private bool fitColliderToVisuals = true;

    [Header("표시 조명")]
    [SerializeField] private bool usePresentationLight = true;
    [SerializeField] private Color presentationLightColor = new Color(1f, 0.68f, 0.38f, 1f);
    [SerializeField, Min(0f)] private float presentationLightIntensity = 0.55f;
    [SerializeField, Min(0.01f)] private float presentationLightMinRange = 0.35f;
    [SerializeField, Min(0.1f)] private float presentationLightRangeScale = 1.35f;
    [SerializeField, Min(0f)] private float presentationLightMinCameraOffset = 0.08f;
    [SerializeField, Min(0f)] private float presentationLightCameraOffsetScale = 0.65f;
    [SerializeField, Range(1f, 2f)] private float hoverLightIntensityMultiplier = 1.18f;
    [SerializeField, Min(0.01f)] private float lightFadeDuration = 0.12f;

    private Light presentationLight;
    private float currentLightIntensity;
    private float targetLightIntensity;
    private bool isHovered;

    private void Awake()
    {
        SetInteractionLayer();
        if (fitColliderToVisuals)
            FitColliderToVisuals();

        PreparePresentationLight();
        ResolveJokboCheckUI();
    }

    private void OnEnable()
    {
        if (presentationLight == null)
            PreparePresentationLight();

        if (presentationLight == null)
            return;

        presentationLight.enabled = usePresentationLight && presentationLightIntensity > 0.001f;
        SetTargetLightIntensity(isHovered);
    }

    private void OnDisable()
    {
        isHovered = false;
        targetLightIntensity = presentationLightIntensity;
        if (presentationLight != null)
            presentationLight.enabled = false;
    }

    private void LateUpdate()
    {
        if (presentationLight == null || !presentationLight.enabled)
            return;

        float maxDelta = Mathf.Max(0.001f, presentationLightIntensity) *
                         Time.unscaledDeltaTime /
                         Mathf.Max(0.01f, lightFadeDuration);
        currentLightIntensity = Mathf.MoveTowards(currentLightIntensity, targetLightIntensity, maxDelta);
        presentationLight.intensity = currentLightIntensity;
        UpdatePresentationLightPosition();
    }

    private void Reset()
    {
        SetInteractionLayer();

        if (fitColliderToVisuals)
            FitColliderToVisuals();
    }

    public override void OnClicked()
    {
        if (!isInteractable || OptionWindowManager.IsAnyOptionWindowOpen())
            return;

        ResolveJokboCheckUI();
        if (jokboCheckUI == null)
            return;

        base.OnClicked();
        jokboCheckUI.OpenPanel();
    }

    public override void OnHoverEnter()
    {
        base.OnHoverEnter();
        isHovered = true;
        SetTargetLightIntensity(true);
    }

    public override void OnHoverExit()
    {
        base.OnHoverExit();
        isHovered = false;
        SetTargetLightIntensity(false);
    }

    private void ResolveJokboCheckUI()
    {
        if (jokboCheckUI == null)
            jokboCheckUI = JokboCheckUI.Instance;
    }

    private void SetInteractionLayer()
    {
        int objectLayer = LayerMask.NameToLayer("Object");
        if (objectLayer >= 0)
            gameObject.layer = objectLayer;
    }

    private void PreparePresentationLight()
    {
        if (!usePresentationLight || presentationLightIntensity <= 0.001f)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].renderingLayerMask |= PresentationLightRenderingLayerMask;
        }

        Transform existingLight = transform.Find(PresentationLightName);
        GameObject lightObject = existingLight != null
            ? existingLight.gameObject
            : new GameObject(PresentationLightName);

        lightObject.transform.SetParent(transform, false);
        lightObject.layer = gameObject.layer;

        presentationLight = lightObject.GetComponent<Light>();
        if (presentationLight == null)
            presentationLight = lightObject.AddComponent<Light>();

        presentationLight.type = LightType.Point;
#if UNITY_EDITOR
        // Bake mode is an editor-only API; runtime-created lights are realtime.
        presentationLight.lightmapBakeType = LightmapBakeType.Realtime;
#endif
        presentationLight.color = presentationLightColor;
        presentationLight.range = ResolvePresentationLightRange();
        presentationLight.shadows = LightShadows.None;
        presentationLight.renderMode = LightRenderMode.ForcePixel;
        presentationLight.bounceIntensity = 0f;
        presentationLight.cullingMask = ~0;
        presentationLight.renderingLayerMask = (int)PresentationLightRenderingLayerMask;
        presentationLight.enabled = true;

        currentLightIntensity = presentationLightIntensity;
        SetTargetLightIntensity(false);
        presentationLight.intensity = currentLightIntensity;
        UpdatePresentationLightPosition();
    }

    private void SetTargetLightIntensity(bool hovered)
    {
        targetLightIntensity = presentationLightIntensity *
                               (hovered ? hoverLightIntensityMultiplier : 1f);
    }

    private float ResolvePresentationLightRange()
    {
        if (!TryGetVisualWorldBounds(out Bounds bounds))
            return presentationLightMinRange;

        return Mathf.Max(presentationLightMinRange, bounds.size.magnitude * presentationLightRangeScale);
    }

    private void UpdatePresentationLightPosition()
    {
        if (presentationLight == null || !TryGetVisualWorldBounds(out Bounds bounds))
            return;

        Vector3 center = bounds.center;
        Camera mainCamera = Camera.main;
        Vector3 towardCamera = mainCamera != null
            ? mainCamera.transform.position - center
            : -transform.forward;

        if (towardCamera.sqrMagnitude <= 0.000001f)
            towardCamera = Vector3.back;

        float cameraOffset = Mathf.Max(
            presentationLightMinCameraOffset,
            bounds.extents.magnitude * presentationLightCameraOffsetScale);
        float verticalOffset = bounds.extents.y * 0.15f;

        presentationLight.transform.position = center +
                                               towardCamera.normalized * cameraOffset +
                                               Vector3.up * verticalOffset;
        presentationLight.transform.rotation = Quaternion.identity;
    }

    private bool TryGetVisualWorldBounds(out Bounds worldBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        worldBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void FitColliderToVisuals()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (boxCollider == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds localBounds = default;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        if (!hasBounds)
            return;

        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;
    }
}
