using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 축 하나에 속한 부품 렌더러를 모아 오류 색으로 깜빡이게 한다.
///
/// 머티리얼을 교체하지 않고 MaterialPropertyBlock으로 색만 덮어쓴다.
/// 덕분에 머티리얼 인스턴스가 생기지 않고(SRP 배칭 유지), 해제할 때
/// SetPropertyBlock(null) 한 번으로 원래 상태로 완전히 되돌아간다.
/// </summary>
public class ErrorHighlighter
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // 머티리얼에 _EMISSION 키워드가 켜져 있을 때만 발광이 보인다. 꺼져 있으면 무시되고
    // _BaseColor 깜빡임만 남으므로, 어느 쪽이든 오류 표시는 눈에 띈다.
    private const float EmissionIntensity = 3f;

    private readonly Renderer[] _renderers;
    private readonly Color[] _originalColors;
    private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    private bool _isHighlighted;

    /// <param name="axis">강조할 축 Transform</param>
    /// <param name="nestedAxes">
    /// 이 축 하위에 들어 있는 다른 축들. 장비는 X ⊃ Y ⊃ Z로 축이 쌓여 있어서
    /// 그냥 하위 렌더러를 전부 모으면 X축 오류에 Z축 그리퍼까지 깜빡인다.
    /// 하위 축 서브트리를 빼야 "그 축 고유 부품"만 남는다.
    /// </param>
    public ErrorHighlighter(Transform axis, IReadOnlyList<Transform> nestedAxes)
    {
        // 매 프레임 GetComponentsInChildren을 부르지 않도록 여기서 한 번만 캐시
        Renderer[] all = axis.GetComponentsInChildren<Renderer>();
        List<Renderer> own = new List<Renderer>(all.Length);

        foreach (Renderer renderer in all)
        {
            if (!IsInsideAny(renderer.transform, nestedAxes))
            {
                own.Add(renderer);
            }
        }

        _renderers = own.ToArray();
        _originalColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Material material = _renderers[i].sharedMaterial;

            _originalColors[i] = material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
        }
    }

    public int RendererCount => _renderers.Length;

    private static bool IsInsideAny(Transform target, IReadOnlyList<Transform> subtrees)
    {
        for (int i = 0; i < subtrees.Count; i++)
        {
            // IsChildOf는 자기 자신도 true로 본다 → 하위 축의 루트까지 함께 제외된다
            if (subtrees[i] != null && target.IsChildOf(subtrees[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 원본 색과 강조 색 사이를 오가며 깜빡인다. 매 프레임 호출.
    /// </summary>
    public void Blink(Color highlightColor, float speed)
    {
        _isHighlighted = true;

        // RotateAndPulse와 같은 파형. 일시정지 중에도 오류는 계속 보여야 하므로 unscaledTime
        float wave = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];

            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(_originalColors[i], highlightColor, wave));
            _block.SetColor(EmissionColorId, highlightColor * (wave * EmissionIntensity));
            renderer.SetPropertyBlock(_block);
        }
    }

    /// <summary>
    /// 강조 해제. 오버라이드를 통째로 걷어내므로 원본 머티리얼 색이 그대로 돌아온다.
    /// </summary>
    public void Clear()
    {
        if (!_isHighlighted)
        {
            return;
        }

        _isHighlighted = false;

        foreach (Renderer renderer in _renderers)
        {
            renderer.SetPropertyBlock(null);
        }
    }

    /// <summary>
    /// 축 전체를 감싸는 월드 바운즈. 카메라 포커스 지점과 마커 위치 산출에 쓴다.
    /// </summary>
    public bool TryGetWorldBounds(out Bounds bounds)
    {
        if (_renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = _renderers[0].bounds;

        for (int i = 1; i < _renderers.Length; i++)
        {
            bounds.Encapsulate(_renderers[i].bounds);
        }

        return true;
    }
}
