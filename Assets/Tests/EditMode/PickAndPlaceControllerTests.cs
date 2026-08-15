using NUnit.Framework;
using UnityEngine;

/// <summary>
/// PickAndPlaceController의 좌표 입력 검증 테스트.
/// MonoBehaviour이지만 Start/Update를 타지 않는 순수 로직만 다루므로 EditMode에서 검증할 수 있다.
/// </summary>
public class PickAndPlaceControllerTests
{
    private const float LimitMm = 1000f;

    private GameObject _gameObject;
    private PickAndPlaceController _controller;

    [SetUp]
    public void SetUp()
    {
        // EditMode에서는 Start/Update가 호출되지 않으므로 축 Transform을 붙이지 않아도 된다.
        _gameObject = new GameObject(nameof(PickAndPlaceControllerTests));
        _controller = _gameObject.AddComponent<PickAndPlaceController>();
        _controller.positionLimitMm = LimitMm;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
    }

    // ---------- IsFinite ----------

    [Test]
    public void IsFinite_정상적인_실수는_true()
    {
        Assert.That(PickAndPlaceController.IsFinite(0f), Is.True);
        Assert.That(PickAndPlaceController.IsFinite(150f), Is.True);
        Assert.That(PickAndPlaceController.IsFinite(-150f), Is.True);
    }

    [Test]
    public void IsFinite_NaN과_무한대는_false()
    {
        Assert.That(PickAndPlaceController.IsFinite(float.NaN), Is.False);
        Assert.That(PickAndPlaceController.IsFinite(float.PositiveInfinity), Is.False);
        Assert.That(PickAndPlaceController.IsFinite(float.NegativeInfinity), Is.False);
    }

    // ---------- ClampToLimit ----------

    [Test]
    public void ClampToLimit_범위_안의_값은_그대로_통과한다()
    {
        Assert.That(_controller.ClampToLimit(0f), Is.EqualTo(0f));
        Assert.That(_controller.ClampToLimit(150f), Is.EqualTo(150f));
        Assert.That(_controller.ClampToLimit(-LimitMm), Is.EqualTo(-LimitMm));
    }

    [Test]
    public void ClampToLimit_범위를_넘으면_상한_하한으로_잘린다()
    {
        Assert.That(_controller.ClampToLimit(999999f), Is.EqualTo(LimitMm));
        Assert.That(_controller.ClampToLimit(-999999f), Is.EqualTo(-LimitMm));
    }

    [Test]
    public void ClampToLimit_상한이_음수로_설정돼도_절대값으로_동작한다()
    {
        _controller.positionLimitMm = -LimitMm;

        Assert.That(_controller.ClampToLimit(999999f), Is.EqualTo(LimitMm));
        Assert.That(_controller.ClampToLimit(-999999f), Is.EqualTo(-LimitMm));
    }

    [Test]
    public void ClampToLimit_NaN은_걸러지지_않는다()
    {
        // Mathf.Clamp는 비교 기반이라 NaN이 그대로 통과한다.
        // 그래서 MoveToPosition이 Clamp보다 앞서 IsFinite로 따로 막는다.
        // 이 동작이 바뀌면 검증 순서를 재검토해야 하므로 계약으로 고정해 둔다.
        Assert.That(float.IsNaN(_controller.ClampToLimit(float.NaN)), Is.True);
    }
}
