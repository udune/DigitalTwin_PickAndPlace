# DigitalTwin_PickAndPlace

픽앤플레이스 장비의 **3D 디지털 트윈 뷰어**입니다. Named Pipe로 [DigitalTwin.Dashboard](https://github.com/udune/DigitalTwin.Dashboard)(Soft-PLC)에 접속해 3축 위치를 실시간 반영하고, 오류 발생 시 카메라가 자동으로 해당 부위를 클로즈업합니다.

원격지에서 장비 화면만 보고도 "어느 축에서 무슨 일이 났는지"를 즉시 파악하는 것이 목표입니다.

---

## 시스템 구성

```
  ┌─────────────────────────┐   Named Pipe    ┌──────────────────────────┐
  │  DigitalTwin.Dashboard  │◄───────────────►│  DigitalTwin_PickAndPlace │
  │      (Soft-PLC)         │  DigitalTwinPipe│      (3D Viewer)          │
  │                         │      JSON       │                          │
  │  100Hz 제어 루프         │                 │  IPCReceiver             │
  │  SLMP 3E / OPC UA 노출   │                 │  → 축 동기화 / 오류 시각화  │
  └─────────────────────────┘                 └──────────────────────────┘
```

---

## 핵심 기능

### 오류 시각화 (Cinemachine)

오류 발생 시 다음이 동시에 일어납니다.

- **카메라 자동 전환** — 오류 카메라 Priority를 5 → 20으로 올려 메인 카메라를 밀어냅니다. 위치는 해당 축 Transform에 오프셋을 더해 실시간 산출하므로, 축이 움직이는 중에도 계속 추적합니다.
- **경고 구체 생성** — 오류 지점 위에 스케일 펄스(`RotateAndPulse`) 효과가 붙은 마커를 배치. 축마다 1개씩 미리 만들어 두고 켜고 끄므로 런타임 할당이 없습니다
- **부품 강조** — 해당 축 렌더러를 `MaterialPropertyBlock`으로 빨갛게 깜빡임(머티리얼 교체 없음, 해제 시 완전 원복). 축이 X ⊃ Y ⊃ Z로 중첩돼 있으므로 **하위 축 부품은 제외**해 문제 축만 강조합니다. 머티리얼에 Emission이 켜져 있으면 발광까지 함께 적용
- **빌보드 라벨** — 오류 메시지가 항상 카메라를 향하도록 (`BillBoard`)
- 오류 해제 시 메인 카메라로 자동 복귀

`Error`가 `Warning`보다 우선하므로, 동시에 여러 오류가 있으면 심각한 쪽을 먼저 비춥니다.

### UI Toolkit 기반 HUD

uGUI에서 UI Toolkit(UXML/USS)으로 전환했습니다. `MainUIController`가 `UIDocument`에서 루트를 받아 세 개의 서브 컨트롤러에 위임하는 구조입니다.

| 컨트롤러 | 담당 |
|---|---|
| `StatusBarController` | 상단 상태 바 — 에러/경고 유무에 따른 색상·문구 |
| `ErrorPanelController` | 오류 목록, 템플릿 인스턴싱, 해제 표시, 이력 삭제 |
| `ConnectionStatusController` | 연결 인디케이터, 연결 중 점멸, 수동 재접속 버튼 |

UXML 5종(`MainUI`, `StatusBar`, `ErrorPanel`, `ErrorItem`, `ConnectionStatus`) + USS 4종으로 구성됩니다.

### 공압 그리퍼

`GripperController`가 진공 흡착과 집게 방식을 모두 지원합니다.

- `gripPoint` 기준 `Physics.OverlapSphere`로 `Pickable` 레이어를 탐색해 가장 가까운 물체 선택
- 레이어 미설정 시 `PickableObject` 컴포넌트 전수 검색으로 폴백
- Pick 시 `Rigidbody.isKinematic = true` 후 그리퍼에 부착, Place 시 물리 복원
- 집게 개폐는 `Lerp` 보간 애니메이션
- `OnDrawGizmosSelected`로 흡착 범위 시각화

### IPC 견고성

Named Pipe 클라이언트는 백그라운드 스레드에서 동작하며, 수신 콜백은 `UnityMainThreadDispatcher`로 메인 스레드에 마샬링합니다.

| 설정 | 기본값 |
|---|---|
| 재연결 간격 | 3초 |
| 최대 재시도 | 0 (무제한) |
| 연결 상태 확인 주기 | 0.5초 |

연결 상태는 `Disconnected / Connecting / Connected` 3단계로 UI에 반영되며, 수동 `Reconnect()`도 제공합니다.

---

## 통신 프로토콜

파이프 이름: `DigitalTwinPipe` / 형식: 줄 단위 JSON

**수신 (Dashboard → Unity)**

| 타입 | 내용 |
|---|---|
| `axis_data` | 위치 X/Y/Z, 속도 X/Y/Z, 타임스탬프 |
| `error` | source, errorType, message, timestamp |
| `clear_all_errors` | 알람 일괄 해제 |
| `gripper_command` | `pick` 또는 `place` |

**송신 (Unity → Dashboard)**

| 타입 | 내용 |
|---|---|
| `axis_data` | 키보드 조작 결과 위치 |
| `gripper_status` | `isGripping`, 잡고 있는 물체 이름 |

단위 변환: **1 Unity unit = 100mm**. 축 매핑은 X→x, Y→z(전후), Z→y(상하)입니다.

---

## 조작

| 키 | 동작 |
|---|---|
| `←` `→` | X축 이동 |
| `↑` `↓` | Y축 이동 |
| `W` `S` | Z축 이동 |
| `G` | 그리퍼 Pick |
| `R` | 그리퍼 Place |
| `Space` | 원점 복귀 + 오류 전체 해제 |

기본 이동 속도 100mm/s. 조작 결과는 즉시 Dashboard로 전송되어 Soft-PLC의 타겟 위치에 반영됩니다.

---

## 오류 판정

Unity는 Dashboard가 보낸 오류를 표시하는 것 외에, `ErrorConditionMonitor`로 자체 경계 판정도 수행합니다.

- 축별 min/max를 인스펙터에서 설정 (Unity unit 기준)
- 범위 이탈 → `Error`
- 범위의 바깥 15% 구간 진입 → `Warning` (`warningThreshold` 0.85)
- 상태 우선순위: Error > Warning > Normal

Dashboard 없이 단독 실행할 때도 오류 시각화를 확인할 수 있게 하기 위한 로컬 판정입니다.

---

## 3D 모델

Blender Python 스크립트로 장비를 절차적으로 생성한 뒤 FBX로 내보냈습니다.

```
PickAndPlaceMachine
├── Frame / Control_Box / Y_Rail
└── X_Rail → X_Carriage → Bridge
              └── Z_AXIS
                    ├── Z_Motor / Z_Mount / Z_Guide_Rail
                    └── Gripper_Left / Gripper_Right
                          └── GripperPoint   ← 물체 부착 지점
```

부모-자식 관계로 축을 쌓아 올려, 상위 축이 움직이면 하위 축이 자연히 따라오도록 했습니다. 머티리얼은 알루미늄·플라스틱·금속 3종.

---

## 기술 스택

| 항목 | 버전 |
|---|---|
| Unity | 6000.4.2f1 |
| 렌더 파이프라인 | URP 17.4.0 |
| 카메라 | Cinemachine 3.1.6 |
| UI | UI Toolkit (UXML / USS) |
| 텍스트 | TextMeshPro |

---

## 실행

```bash
git clone https://github.com/udune/DigitalTwin_PickAndPlace.git
```

Unity Hub에서 6000.4.2f1로 열고 `Assets/Scenes/Main.unity`를 실행합니다.

Dashboard와 연동하려면 **WPF 앱을 먼저 실행하고 `▶ START`를 누른 뒤** Unity를 재생하세요. 순서가 반대여도 3초 간격 재연결로 결국 붙지만, 초기 연결이 빠릅니다. Dashboard 없이 단독 실행해도 키보드 조작과 로컬 오류 판정은 동작합니다.

---

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── IPCReceiver.cs              Named Pipe 클라이언트 + 프로토콜
│   ├── PickAndPlaceController.cs   축 Transform 보간
│   ├── AxisMover.cs                키보드 입력
│   ├── ErrorVisualizer.cs          오류 시각화 조정자 (카메라 + 마커 + 부품 강조)
│   ├── BillBoard.cs / RotateAndPulse.cs
│   ├── UnityMainThreadDispatcher.cs
│   ├── Core/GripperController.cs
│   ├── Interaction/PickableObject.cs
│   ├── Error/                      ErrorManager, ErrorConditionMonitor, ErrorData
│   │                               ErrorMarker(경고 구체+라벨), ErrorHighlighter(부품 깜빡임)
│   └── UI/MainUIController.cs      + 3개 서브 컨트롤러
├── UI/
│   ├── Documents/                  UXML 5종
│   └── Styles/                     USS 4종
├── Prefabs/
└── Scenes/Main.unity
```

### 개발 도구

`.claude/skills/`에 프로젝트 전용 Claude Code 스킬 2종을 두었습니다.

- `editor_check` — Hierarchy/Inspector 설정이 C# 코드의 의도와 일치하는지 검사. Unity는 인스펙터 참조 누락이 컴파일 에러 없이 런타임에만 터지기 때문에 이를 사전 점검합니다.
- `simplify` — KISS 원칙 기반 리팩토링 보조

---

## 알려진 제약

- 그리퍼 Pick/Place는 Unity 키보드로만 조작 가능합니다. `gripper_command` 수신 로직은 구현돼 있으나 Dashboard 측 송신 UI가 아직 없습니다.
- `ErrorConditionMonitor`의 경계값은 Unity unit 기준이라 Dashboard의 알람 경계(mm)와 별개로 관리됩니다. 두 판정이 동시에 켜지면 임계값이 어긋날 수 있어, 향후 단일화가 필요합니다.
- Input System 패키지가 설치돼 있으나 입력은 레거시 `Input` 클래스를 사용합니다.
- `PickAndPlaceController`는 수신 위치로 `Lerp` 보간하므로 화면상 위치가 실제 PLC 위치보다 약간 지연됩니다(의도된 시각적 스무딩).

## 로드맵

- [ ] OpenCVSharp 연동 — 특정 형상 물체만 선별 픽업
- [ ] 자동 픽앤플레이스 사이클
- [ ] 오류 판정 기준 Dashboard 단일화
- [ ] WebGL 빌드 배포

## 연관 리포지토리

[DigitalTwin.Dashboard](https://github.com/udune/DigitalTwin.Dashboard) — Soft-PLC 및 감시 대시보드 (WPF, SLMP 3E, OPC UA)
