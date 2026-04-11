# 디지털 트윈 Pick & Place - UI 개선 및 조건 기반 에러 시스템 구현 계획

## 개요

두 가지 목표를 달성합니다:
1. **산업용 SCADA 스타일 UI** - 어두운 배경, 상단 상태바, 우측 에러 리스트 패널, 심각도별 색상 구분
2. **조건 기반 에러 시스템** - 실제 축 리미트 감시, Warning/Error 2단계, 조건 유지시 에러 유지, 해소시 자동 클리어

### 요구사항 확인
- 다중 에러 동시 지원 (리스트로 표시, 카메라는 가장 심각한 에러에 포커스)
- 경고 + 에러 2단계 (리미트 접근 시 Warning, 초과 시 Error)
- 각 축 리미트값 Inspector에서 설정 가능
- 산업용 SCADA 스타일 UI

---

## 새 파일 구조

```
Assets/Scripts/
    Error/
        ErrorData.cs              -- Enum, Struct (데이터 모델)
        ErrorManager.cs           -- Singleton, 활성 에러 관리, 이벤트
        ErrorConditionMonitor.cs  -- 매 프레임 축 리미트 감시
    UI/
        ErrorUIManager.cs         -- UI 캔버스 전체 관리
        ErrorEntryUI.cs           -- 에러 리스트의 개별 항목
        UIAnimationHelper.cs      -- Fade/Slide 애니메이션 헬퍼
```

---

## Step 1: `ErrorData.cs` (신규)

경로: `Assets/Scripts/Error/ErrorData.cs`

```csharp
public enum ErrorSeverity { Warning, Error }
public enum ErrorSource { XAxis, YAxis, ZAxis, Gripper }

public struct ErrorInfo
{
    public string Id;              // "XAxis_Limit", "YAxis_Warning" 등 고유 키
    public ErrorSeverity Severity;
    public ErrorSource Source;
    public string Location;        // "X-AXIS" 등 표시명
    public string Message;         // "X축 리미트 초과 (현재: 1.25)"
    public float Timestamp;        // Time.time
}
```

---

## Step 2: `ErrorManager.cs` (신규)

경로: `Assets/Scripts/Error/ErrorManager.cs`

- Singleton MonoBehaviour (`public static ErrorManager Instance`)
- `Dictionary<string, ErrorInfo>` 로 활성 에러 관리
- API: `RaiseError(ErrorInfo)`, `ClearError(string id)`, `ClearAllErrors()`
- Properties: `ActiveErrors`, `HasErrors`, `HasWarnings`
- Events: `OnErrorRaised`, `OnErrorCleared`, `OnAllCleared`
- `RaiseError`는 같은 Id가 이미 있으면 무시 (중복 방지)
- 타이머 기반 자동 클리어 없음 — 조건 모니터가 명시적으로 클리어

---

## Step 3: `ErrorConditionMonitor.cs` (신규)

경로: `Assets/Scripts/Error/ErrorConditionMonitor.cs`

Inspector 설정 필드:
- `xAxis, yAxis, zAxis` Transform 참조
- 축별 `min, max` 리미트값
- `warningThreshold` (0~1, 기본 0.85) — 범위의 85%에서 Warning

Update() 로직 (축별 동일 패턴):
```
위치 > max 또는 위치 < min → Error 발생, Warning 클리어
위치가 Warning 구간 내 → Warning 발생, Error 클리어
위치가 정상 범위 → Error + Warning 모두 클리어
```

히스테리시스 적용: 에러 클리어 시 약간의 여유값(0.02f)을 두어 경계에서 깜빡임 방지

---

## Step 4: `AxisMover.cs` 수정

경로: `Assets/Scripts/AxisMover.cs` (기존 파일)

변경사항:
- Space키 리셋 시 `ErrorManager.Instance?.ClearAllErrors()` 호출 추가
- 기존 이동 로직은 그대로 유지 (리미트 초과 허용하여 에러 트리거 가능)

---

## Step 5: UI 애니메이션 - `UIAnimationHelper.cs` (신규)

경로: `Assets/Scripts/UI/UIAnimationHelper.cs`

정적 코루틴 메서드:
- `FadeIn(CanvasGroup, duration)` / `FadeOut(CanvasGroup, duration)`
- `SlideIn(RectTransform, distance, duration)` / `SlideOut(...)`
- `Time.unscaledDeltaTime` 사용 (TimeScale 영향 안 받음)

---

## Step 6: `ErrorEntryUI.cs` (신규)

경로: `Assets/Scripts/UI/ErrorEntryUI.cs`

에러 리스트의 개별 항목 프리팹에 부착:
- `severityBadge` (Image) — Error: 빨강, Warning: 주황
- `locationText` (TMP) — "X-AXIS"
- `messageText` (TMP) — 에러 메시지
- `leftBorder` (Image) — 4px 좌측 컬러 바
- `canvasGroup` — 페이드 애니메이션용
- `Setup(ErrorInfo, Color)` 메서드

---

## Step 7: `ErrorUIManager.cs` (신규)

경로: `Assets/Scripts/UI/ErrorUIManager.cs`

UI 전체 관리:

### 상태바 (StatusBar) — 화면 상단
- 배경색: 정상=`#1B5E20`, 경고=`#F57F17`, 에러=`#B71C1C`
- 텍스트: "SYSTEM NORMAL" / "WARNING" / "ERROR"
- 좌측에 상태 아이콘 (원형)

### 에러 리스트 패널 — 화면 우측
- 어두운 반투명 배경 `#0D1117` alpha 0.85
- VerticalLayoutGroup으로 에러 항목 나열
- 에러 발생 시 슬라이드+페이드 인, 해소 시 페이드 아웃

### 이벤트 구독
- `ErrorManager.OnErrorRaised` → 에러 항목 생성 및 애니메이션
- `ErrorManager.OnErrorCleared` → 해당 항목 페이드 아웃 후 Destroy
- 활성 에러 없으면 패널 자동 숨김

### 색상 테마
| 요소 | Error | Warning |
|------|-------|---------|
| 상태바 배경 | `#B71C1C` | `#F57F17` |
| 심각도 뱃지 | `#EF5350` | `#FFB74D` |
| 좌측 보더 | `#EF5350` | `#FFB74D` |
| 패널 배경 | `#0D1117` alpha 0.85 | 동일 |
| 텍스트 | 흰색 | 흰색 |

---

## Step 8: `ErrorVisualizer.cs` 리팩토링

경로: `Assets/Scripts/ErrorVisualizer.cs` (기존 파일)

변경사항:
- UI 관련 필드 제거 (`errorMessage`, `errorPanel`)
- ContextMenu 테스트 메서드 제거
- 이벤트 기반으로 전환: `ErrorManager.OnErrorRaised` / `OnErrorCleared` / `OnAllCleared` 구독
- 카메라 포커스 전용 역할:
  - 에러 발생 → 가장 심각한 에러의 Source Transform으로 카메라 이동
  - 모든 에러 해소 → 카메라 우선순위 복원
- `GetTargetCenter()` 로직은 기존 유지
- `GetTransformForSource(ErrorSource)` — enum 기반 매핑으로 교체

---

## Step 9: 씬 설정 (Unity Editor 작업)

1. `ErrorManager` 빈 GameObject 생성, ErrorManager 컴포넌트 부착
2. `ErrorConditionMonitor` 빈 GameObject 생성, 축 참조 및 리미트값 설정
3. `ErrorCanvas` 수정:
   - 기존 `ErrorStatusPanel` + `LogText` 제거
   - 새 StatusBar (상단), ErrorListPanel (우측) 추가
   - `ErrorUIManager` 컴포넌트 부착 및 참조 연결
4. `ErrorEntryPrefab` 프리팹 생성 (`Assets/Prefabs/UI/`)
5. `ErrorVisualizer`에서 UI 참조 제거, 카메라 참조만 유지

> 씬 설정은 코드로 할 수 없으므로 스크립트 완성 후 사용자가 Unity Editor에서 수행해야 합니다.

---

## 수정 대상 파일 요약

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Error/ErrorData.cs` | **신규** 생성 |
| `Assets/Scripts/Error/ErrorManager.cs` | **신규** 생성 |
| `Assets/Scripts/Error/ErrorConditionMonitor.cs` | **신규** 생성 |
| `Assets/Scripts/UI/ErrorUIManager.cs` | **신규** 생성 |
| `Assets/Scripts/UI/ErrorEntryUI.cs` | **신규** 생성 |
| `Assets/Scripts/UI/UIAnimationHelper.cs` | **신규** 생성 |
| `Assets/Scripts/ErrorVisualizer.cs` | **리팩토링** — 카메라 전용으로 축소 |
| `Assets/Scripts/AxisMover.cs` | **수정** — Space 리셋 시 에러 클리어 연동 |
| `Assets/Scripts/BillBoard.cs` | 변경 없음 |
| `Assets/Scripts/RotateAndPulse.cs` | 변경 없음 |

---

## 검증 방법

1. **조건 모니터 테스트**: Play 모드에서 화살표 키로 축을 리미트 밖으로 이동 → Warning/Error 자동 발생 확인
2. **에러 지속 테스트**: 리미트 밖에서 에러가 계속 유지되는지 확인
3. **에러 해소 테스트**: 축을 다시 범위 안으로 이동 → 에러 자동 클리어 확인
4. **다중 에러 테스트**: X축 + Y축 동시 리미트 초과 → 리스트에 2개 표시 확인
5. **UI 애니메이션**: 에러 발생/해소 시 슬라이드+페이드 애니메이션 확인
6. **상태바 색상**: 정상=녹색, Warning만=주황, Error있음=빨강 전환 확인
7. **카메라 포커스**: 에러 발생 시 해당 축으로 카메라 이동, 해소 시 원복 확인
8. **Space 리셋**: Space키로 모든 축 원점 복귀 + 에러 전부 클리어 확인
