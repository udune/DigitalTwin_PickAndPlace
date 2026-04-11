# Unity Editor Hierarchy Check Skill

Unity Editor의 Hierarchy/Inspector 설정이 C# 코드의 의도와 일치하는지 분석하고, 누락되거나 잘못된 설정을 찾아 개선합니다.

## Skill 실행 조건

사용자가 다음과 같이 요청할 때 이 skill을 실행합니다:
- "에디터 체크해줘", "씬 설정 확인해줘"
- "하이어라키 검토해줘", "Inspector 확인해줘"
- `/editor_check` 명령 사용 시

## 분석 목적

**C# 스크립트에서 요구하는 설정**과 **Unity Scene의 실제 설정**을 비교하여:
1. 누락된 컴포넌트/오브젝트 찾기
2. 미할당된 Inspector 필드 찾기
3. 잘못된 레이어/태그 설정 찾기
4. GameObject 구조 검증

## 분석 대상

### C# 스크립트 (Inspector 필드 추출)
```
Assets/Scripts/
├── PickAndPlaceController.cs    ← xAxis, yAxis, zAxis Transform
├── AxisMover.cs                 ← controller, gripper 참조
├── IPCReceiver.cs               ← controller, gripper 참조
├── Core/GripperController.cs    ← gripperLeft, gripperRight, gripPoint
├── Interaction/PickableObject.cs ← Rigidbody, Collider 필수
├── ErrorVisualizer.cs           ← 카메라, Cinemachine 참조
└── UI/MainUIController.cs       ← UIDocument 참조
```

### Scene 파일 (실제 설정 확인)
```
Assets/Scenes/Main.unity
```

### Project Settings (레이어/태그)
```
ProjectSettings/TagManager.asset
```

## 분석 절차

### Step 1: 코드에서 요구사항 추출

각 스크립트의 public/SerializeField 필드를 분석합니다:

```csharp
// 예: GripperController.cs에서 추출
public Transform gripperLeft;      // → GRIPPER 하위에 GripperLeft 필요
public Transform gripperRight;     // → GRIPPER 하위에 GripperRight 필요
public Transform gripPoint;        // → GRIPPER 하위에 GripPoint 필요
```

### Step 2: Scene 파일 분석

Unity Scene 파일(.unity)은 YAML 형식입니다. 다음을 확인합니다:

```yaml
# MonoBehaviour 컴포넌트의 필드 값 확인
MonoBehaviour:
  m_Script: {fileID: ..., guid: ...}  # 어떤 스크립트인지
  controller: {fileID: 0}              # 0이면 미할당!
  gripper: {fileID: 123456}            # 할당됨
```

### Step 3: 요구사항 vs 실제 설정 비교

| 스크립트 | 필드 | 요구사항 | 실제 상태 |
|---------|------|---------|----------|
| AxisMover | controller | PickAndPlaceController 참조 | ✅/❌ |
| AxisMover | gripper | GripperController 참조 | ✅/❌ |
| GripperController | gripPoint | Empty Transform | ✅/❌ |

### Step 4: 레이어/태그 검증

```
필요한 레이어:
- Pickable (PickableObject에서 사용)

필요한 태그:
- (현재 없음)
```

## 검증 체크리스트

### 1. 기계 제어 시스템
```
□ PickAndPlaceController가 씬에 존재
  □ xAxis Transform 할당됨
  □ yAxis Transform 할당됨
  □ zAxis Transform 할당됨

□ AxisMover가 씬에 존재
  □ controller 필드에 PickAndPlaceController 할당됨
  □ gripper 필드에 GripperController 할당됨
```

### 2. 그리퍼 시스템
```
□ GRIPPER GameObject 존재
  □ GripperController 컴포넌트 부착됨
  □ gripperLeft (GripperLeft Transform) 할당됨
  □ gripperRight (GripperRight Transform) 할당됨
  □ gripPoint (GripPoint Transform) 할당됨

□ GripperLeft GameObject 존재
  □ GRIPPER의 자식임
  □ Box Collider 없음 (제거됨)

□ GripperRight GameObject 존재
  □ GRIPPER의 자식임
  □ Box Collider 없음 (제거됨)

□ GripPoint GameObject 존재
  □ GRIPPER의 자식임
  □ Empty Transform임
```

### 3. Pickable 물체
```
□ "Pickable" 레이어가 TagManager에 존재

□ Pickable 물체 (Cube_01 등)
  □ Layer가 "Pickable"로 설정됨
  □ PickableObject 컴포넌트 부착됨
  □ Rigidbody 컴포넌트 존재
    □ Use Gravity: true
    □ Is Kinematic: false
  □ Collider 컴포넌트 존재
```

### 4. 통신 시스템
```
□ IPCReceiver가 씬에 존재
  □ controller 필드 할당됨
  □ gripper 필드 할당됨
```

### 5. 에러 시스템
```
□ ErrorManager가 씬에 존재 (또는 자동 생성)
□ ErrorConditionMonitor가 씬에 존재
□ ErrorVisualizer가 씬에 존재
```

### 6. UI 시스템
```
□ UIDocument가 씬에 존재
  □ Panel Settings 할당됨
  □ Source Asset (MainUI.uxml) 할당됨
□ MainUIController가 씬에 존재
```

## 출력 형식

```markdown
# 🔍 Unity Editor 설정 검증 결과

## 📊 요약
- 검증 항목: X개
- 정상: X개 ✅
- 문제: X개 ❌
- 경고: X개 ⚠️

## ❌ 누락/오류 항목

### [문제 1] GripperController.gripPoint 미할당
- **위치**: Hierarchy > GRIPPER > GripperController
- **현재 상태**: None (미할당)
- **필요한 조치**:
  1. GRIPPER 하위에 Empty GameObject "GripPoint" 생성
  2. GripperController의 Grip Point 필드에 드래그

### [문제 2] "Pickable" 레이어 미생성
- **위치**: Edit > Project Settings > Tags and Layers
- **현재 상태**: 레이어 없음
- **필요한 조치**:
  1. Tags and Layers 열기
  2. User Layer 8에 "Pickable" 입력

## ⚠️ 경고 항목

### [경고 1] GripperLeft에 Box Collider 존재
- **위치**: Hierarchy > GRIPPER > GripperLeft
- **문제**: Pick 시 물체와 충돌하여 튕김 발생 가능
- **권장 조치**: Box Collider 컴포넌트 제거

## ✅ 정상 항목
- PickAndPlaceController: 모든 축 Transform 할당됨
- AxisMover: controller 할당됨
- IPCReceiver: 연결 설정 정상

## 🔧 설정 가이드

### GRIPPER 구조 (권장)
```
GRIPPER (Empty + GripperController)
├── GripperLeft (Cube, Collider 제거)
│   Position: (-0.02, 0, 0)
├── GripperRight (Cube, Collider 제거)
│   Position: (0.02, 0, 0)
└── GripPoint (Empty)
    Position: (0, 0, 0)
```

### Inspector 설정값
| 컴포넌트 | 필드 | 권장값 |
|---------|------|-------|
| GripperController | Open Distance | 0.05 |
| GripperController | Close Distance | 0.01 |
| GripperController | Vacuum Range | 0.03 |
```

## 분석 방법

### Scene 파일(.unity) YAML 분석
```yaml
# GameObject 찾기
GameObject:
  m_Name: GRIPPER

# 컴포넌트 찾기
MonoBehaviour:
  m_Script: {guid: [GripperController GUID]}
  gripperLeft: {fileID: 0}  # 0 = 미할당
  gripperRight: {fileID: 123}  # 할당됨
```

### TagManager.asset 분석
```yaml
# 레이어 확인
layers:
  - Default
  - ...
  - Pickable  # User Layer에 존재하는지
```

## 사용자 상호작용

### 스크린샷 요청 시
Scene 파일로 확인이 어려운 경우:

```
Claude: Inspector 설정을 확인하기 위해 스크린샷이 필요합니다.
        GRIPPER 오브젝트를 선택하고 Inspector 창을 캡처해주세요.
```

### 설정 안내
문제 발견 시 단계별 설정 방법을 제공합니다:

```
1. Hierarchy에서 GRIPPER 선택
2. Inspector에서 GripperController 확인
3. Grip Point 필드 옆 ⊙ 클릭
4. GripPoint 검색하여 선택
```

## 실행 예시

```
사용자: 에디터 체크해줘

Claude:
1. C# 스크립트에서 public 필드 분석
2. Main.unity Scene 파일 읽기
3. TagManager.asset에서 레이어 확인
4. 요구사항 vs 실제 설정 비교
5. 문제점/누락 항목 보고
6. 설정 가이드 제공
```

## 자동화 한계

다음 항목은 Scene 파일 분석으로 확인 가능:
- ✅ 컴포넌트 존재 여부
- ✅ 필드 할당 여부 (fileID 확인)
- ✅ GameObject 이름/구조
- ✅ 레이어/태그 설정

다음 항목은 스크린샷 또는 사용자 확인 필요:
- ❓ 정확한 Transform 값
- ❓ Material 설정
- ❓ 실행 중 동작 확인
