# 운패록 — 팀 프로젝트 개발 참여 기록

카드 조합과 기물 효과를 이용해 목표 점수를 달성하는 게임입니다.

원본 팀 저장소는 비공개이며, 여기에는 관련 스크립트 54개를 선별해 포함했으며, Unity 프로젝트 전체와 실행 빌드는 포함하지 않습니다.

**본인 기여의 중심은 SO 기반 데이터 구조와 게임 시스템·UI의 기본 틀 제작입니다.** SO와 초기 게임 시스템의 기본 틀을 포함해 여러 기능의 구현·수정 과정에서 AI 도구를 활용했습니다. AI 활용 정도는 기능마다 다릅니다.

아래 코드는 팀원의 후속 확장도 포함한 현재 버전입니다. 설명은 참여한 기능과 현재 코드의 동작을 소개하며, 모든 코드를 직접 작성하거나 모든 동작을 단독 구현했다는 의미는 아닙니다.

## 프로젝트 정보

| 항목 | 내용 |
| --- | --- |
| 프로젝트명 | 운패록 |
| 개발 기간 | 2025.12.18 ~ 진행 중 |
| 개발 환경 | Unity / C# |
| 팀 구성 | 6인 |
| 담당 범위 | AI 도구를 일부 활용한 ScriptableObject 기반 기본 데이터 구조와 LuTestFolder 내 게임 시스템·UI의 기본 틀 제작 |

## 개발 참여 범위

원본 프로젝트의 `Assets/LuTestFolder/`에 있는 게임 시스템과 UI의 기본적인 틀을 만드는 데 참여했으며, 이 과정에서도 AI 도구의 도움을 받았습니다.
아래에서는 기본 틀 작성과 참여한 기능의 동작을 중심으로 소개합니다.

- LuTestFolder 내 카드·전투·UI 시스템의 기본 틀 제작
- ScriptableObject를 활용한 일반 카드·부적·조커·보스 디버프·족보 강화 카드·카드 팩 데이터 구성
- 카드 관리, 점수 계산, 전투 진행과 소모품·팩 사용 기능의 기본 구조
- 옵션과 족보 두루마리 UI
- 전투 보상과 게임 종료 결과 표시
- 튜토리얼·도감 제작 참여

## 주요 구현

### 1. ScriptableObject 기반 게임 데이터 구성

AI 도구의 도움을 일부 받아 카드, 부적, 조커와 보스 디버프 등의 ScriptableObject 기반 데이터 구조를 구성했습니다.
카드 계열은 공통 기반인 [BaseCardSO](Sources/Assets/LuTestFolder/Scripts/SO/BaseCardSO.cs)를 상속하고, 종류별 정보는 각 데이터 타입에 정의했습니다.

| 데이터 타입 | 역할과 주요 정보 |
| --- | --- |
| [BaseCardSO](Sources/Assets/LuTestFolder/Scripts/SO/BaseCardSO.cs) | 카드 ID, 이름, 카드 종류를 정의하는 공통 기반 |
| [CardSO](Sources/Assets/LuTestFolder/Scripts/SO/CardSO.cs) | 일반 카드의 월, 끗값, 광 여부, 기본 점수, 강화·에디션과 이미지 정보 |
| [AmuletSO](Sources/Assets/LuTestFolder/Scripts/SO/AmuletSO.cs) | 부적 종류, 설명, 이미지·모델, 사용 대상 카드 수와 효과 처리 |
| [JokerSO](Sources/Assets/LuTestFolder/Scripts/JokerAbil/JokerSO.cs) | 조커 이름·설명·이미지, 기본 칩, 해금 조건과 능력 목록 |
| [BossDebuffSO](Sources/Assets/LuTestFolder/Scripts/SO/BossDebuffSO.cs) | 보스 디버프 이름·설명과 초기화·적용·해제 처리를 위한 공통 기반 |
| [PlanetSO](Sources/Assets/LuTestFolder/Scripts/SO/PlanetSO.cs) | 강화 대상 족보와 증가하는 칩·배수 정보 |
| [CardpackSO](Sources/Assets/LuTestFolder/Scripts/SO/CardpackSO.cs) | 팩 종류·크기·가격, 생성할 후보 수와 선택 가능한 수 |

콘텐츠별 데이터를 에셋으로 구성해 Inspector에서 확인하고 수치를 설정할 수 있도록 했습니다.
새로운 효과 동작에는 별도 코드가 필요하므로, 모든 콘텐츠를 데이터 변경만으로 추가할 수 있는 구조라고 표현하지 않습니다.

보스 디버프는 공통 기반을 상속해 손패 감소, 요구 점수 증가, 특정 월 카드 무효화 등의 효과를 구분합니다.

### 2. 카드·전투와 소모품 사용의 기본 구조

`Assets/LuTestFolder/`에서 데이터가 실제 게임 진행에 연결되도록 AI 도구를 일부 활용해 카드·전투·UI의 기본 틀을 제작했습니다.
아래는 관련 매니저가 담당하는 기능입니다. 이후 추가·변경된 현재 코드 전체를 단독 작성했다는 의미는 아닙니다.

| 구성 요소 | 담당 기능 |
| --- | --- |
| [CardManager_2](Sources/Assets/LuTestFolder/Scripts/Manager/CardManager_2.cs) | 덱·손패 관리, 카드 뽑기·선택·제출·버리기 |
| [ScoreManager](Sources/Assets/LuTestFolder/Scripts/Manager/ScoreManager.cs) | 제출한 카드의 족보 판정과 칩·배수를 이용한 점수 계산 |
| [CombatManager](Sources/Assets/LuTestFolder/Scripts/Manager/CombatManager.cs) | 목표 점수, 제출·버리기 횟수, 전투 클리어·패배와 보스 디버프 처리 |
| [ConsumableManager](Sources/Assets/LuTestFolder/Scripts/Manager/ConsumableManager.cs) | 부적 보유 목록과 추가·사용·제거 처리 |
| [PlanetCardLevelManager](Sources/Assets/LuTestFolder/Scripts/Manager/PlanetCardLevelManager.cs) | 족보별 레벨과 칩·배수 강화값 관리 |
| [PackOpeningManager](Sources/Assets/LuTestFolder/Scripts/Manager/PackOpeningManager.cs) | 팩 데이터에 따른 후보 생성과 선택 수 관리 |

라운드의 목표 점수, 제출·버리기 횟수와 보스 여부는 `RoundData`로 구분합니다.
`RoundData`는 ScriptableObject가 아닌 직렬화 가능한 일반 데이터 클래스입니다.

### 3. 옵션과 족보 정보 UI

아래는 현재 옵션과 족보 UI에서 사용하는 스크립트별 역할입니다. 초기 UI 틀과 후속 변경이 함께 포함되어 있습니다.

**옵션 창**

| 구성 요소 | 담당 기능 |
| --- | --- |
| [OptionWindowManager](Sources/Assets/LuTestFolder/Scripts/UI/111/OptionWindowManager.cs) | 옵션 창 열기·닫기, 메뉴·하위 패널 전환, ESC 처리와 게임 입력 차단·해제 |
| [OptionTabButton](Sources/Assets/LuTestFolder/Scripts/UI/111/OptionTabButton.cs) | 탭 버튼의 선택·호버 상태에 따른 배경과 글자 색상 표시 |
| [GameOptionPanel](Sources/Assets/LuTestFolder/Scripts/UI/111/GameOptionPanel.cs) | 연출 배속 선택, 선택 단계 표시와 설정값 저장·적용 |
| [VideoOptionPanel](Sources/Assets/LuTestFolder/Scripts/UI/111/VideoOptionPanel.cs) | 화면 모드·해상도 변경과 감마 조절, 현재 설정값 표시 |
| [SoundPanelManager](Sources/Assets/LuTestFolder/Scripts/UI/111/SoundPanelManager.cs) | 사운드 패널을 열 때 저장된 음량 설정을 각 조작 UI에 반영 |
| [VolumeController](Sources/Assets/LuTestFolder/Scripts/UI/111/VolumeController.cs) | 전체·BGM·효과음 음량의 슬라이더·숫자 입력 동기화, 적용과 저장 |

- 창을 열면 카드·게임플레이 입력을 차단합니다.
- 설정 메뉴를 이동하고 ESC로 이전 화면으로 돌아가거나 창을 닫습니다.
- 창을 닫거나 비활성화할 때 해당 창이 확보한 입력 차단을 해제합니다.

**족보 두루마리**

| 구성 요소 | 담당 기능 |
| --- | --- |
| [JokboScrollClickTarget](Sources/Assets/LuTestFolder/Scripts/UI/JokboScrollClickTarget.cs) | 두루마리 오브젝트 클릭을 족보 패널 열기로 연결 |
| [JokboCheckUI](Sources/Assets/LuTestFolder/Scripts/UI/JokboCheckUI.cs) | 두루마리·패널 열기·닫기와 연출, 전체 행 갱신, 카드·시점 입력 차단·해제 |
| [JokboRowUI](Sources/Assets/LuTestFolder/Scripts/UI/JokboRowUI.cs) | 족보별 이름·레벨·칩·배수·사용 횟수를 읽어 한 행의 텍스트 갱신 |

- 두루마리 오브젝트를 눌러 족보 확인 화면을 엽니다.
- 족보별 현재 칩, 배수, 이번 런의 사용 횟수를 표시합니다.
- 족보 규칙과 강화 데이터에서 표시값을 가져옵니다.
- 화면을 사용하는 동안 카드·시점 입력을 차단하고 종료 시 해제합니다.

표시 데이터는 [ScoreManager](Sources/Assets/LuTestFolder/Scripts/Manager/ScoreManager.cs)의 족보 규칙, [PlanetCardLevelManager](Sources/Assets/LuTestFolder/Scripts/Manager/PlanetCardLevelManager.cs)의 강화값, [JokboPlayTracker](Sources/Assets/LuTestFolder/Scripts/Manager/JokboPlayTracker.cs)의 이번 런 사용 횟수에서 가져옵니다.

### 4. 전투 보상과 게임 종료 결과

관련 코드: [BattleResultUI](Sources/Assets/LuTestFolder/Scripts/UI/BattleResultUI.cs), [BattleResultPanelView](Sources/Assets/LuTestFolder/Scripts/UI/BattleResultPanelView.cs), [GameResultUIManager](Sources/Assets/LuTestFolder/Scripts/Manager/GameResultUIManager.cs)

결과창의 기초 구현에 참여했으며, 아래는 후속 확장을 포함한 현재 동작입니다.

전투 결과창은 기본 보상, 이자, 기물, 시너지, 제령 보상을 구분해 표시합니다. 보상이 0인 상세 행은 숨기고 남은 행의 배치를 갱신하며, 총합은 별도 행으로 표시합니다.

게임 종료 결과창은 최고 제출 점수, 최다 사용 족보, 카드 제출·버리기 횟수, 재화 소비와 구매·리롤 횟수 등을 표시합니다. 승리와 패배에 따라 결과 화면을 구분합니다.

### 5. 플레이어 행동에 맞춘 튜토리얼

관련 코드: [TutorialDirector](Sources/Assets/Scripts/Tutorial/TutorialDirector.cs)

튜토리얼은 Codex를 활용해 코드가 작성된 기능입니다. 아래는 현재 기능의 진행 흐름입니다.

상점 튜토리얼은 상품 설명 확인, 지정 기물 구매, 새로고침, 결과 안내 순서로 진행합니다.

- 현재 단계의 조작 대상을 강조하고 허용할 입력을 구분합니다.
- 구매 성공과 리롤 완료 이벤트를 확인해 다음 단계로 진행합니다.
- 안내 종료 후 해당 단계의 입력 제한을 해제합니다.

### 6. 도감

도감은 AI를 활용해 제작한 기능이며, 제작 경험으로 간단히 소개합니다.

## 관련 코드 구성

위의 스크립트 이름 링크에서 실제 코드를 볼 수 있습니다. [Sources 폴더](Sources/)에는 원본 상대 경로를 유지한 C# 스크립트 54개가 있습니다.

| 영역 | 원본 코드 위치 |
| --- | --- |
| 공통 카드·일반 카드 | [Assets/LuTestFolder/Scripts/SO/BaseCardSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/BaseCardSO.cs), [Assets/LuTestFolder/Scripts/SO/CardSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/CardSO.cs) |
| 부적 | [Assets/LuTestFolder/Scripts/SO/AmuletSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/AmuletSO.cs) |
| 조커 | [Assets/LuTestFolder/Scripts/JokerAbil/JokerSO.cs](Sources/Assets/LuTestFolder/Scripts/JokerAbil/JokerSO.cs) |
| 보스 디버프 | [Assets/LuTestFolder/Scripts/SO/BossDebuffSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/BossDebuffSO.cs), [Assets/LuTestFolder/Scripts/JokerDebuff/](Sources/Assets/LuTestFolder/Scripts/JokerDebuff/) |
| 족보 강화 카드 | [Assets/LuTestFolder/Scripts/SO/PlanetSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/PlanetSO.cs) |
| 카드 팩 | [Assets/LuTestFolder/Scripts/SO/CardpackSO.cs](Sources/Assets/LuTestFolder/Scripts/SO/CardpackSO.cs) |
| 카드·전투 | [Assets/LuTestFolder/Scripts/Manager/CardManager_2.cs](Sources/Assets/LuTestFolder/Scripts/Manager/CardManager_2.cs), [CombatManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/CombatManager.cs) |
| 점수 계산 | [Assets/LuTestFolder/Scripts/Manager/ScoreManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/ScoreManager.cs) |
| 소모품 관리 | [Assets/LuTestFolder/Scripts/Manager/ConsumableManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/ConsumableManager.cs) |
| 족보 강화 관리 | [Assets/LuTestFolder/Scripts/Manager/PlanetCardLevelManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/PlanetCardLevelManager.cs) |
| 팩 개봉 | [Assets/LuTestFolder/Scripts/Manager/PackOpeningManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/PackOpeningManager.cs) |
| 옵션 | [Assets/LuTestFolder/Scripts/UI/111/OptionWindowManager.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/OptionWindowManager.cs) |
| 옵션 설정·버튼 | `Assets/LuTestFolder/Scripts/UI/111/`의 [GameOptionPanel.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/GameOptionPanel.cs), [VideoOptionPanel.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/VideoOptionPanel.cs), [SoundPanelManager.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/SoundPanelManager.cs), [VolumeController.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/VolumeController.cs), [OptionTabButton.cs](Sources/Assets/LuTestFolder/Scripts/UI/111/OptionTabButton.cs) |
| 족보 확인 | [Assets/LuTestFolder/Scripts/UI/JokboCheckUI.cs](Sources/Assets/LuTestFolder/Scripts/UI/JokboCheckUI.cs) |
| 족보 행·클릭 | `Assets/LuTestFolder/Scripts/UI/`의 [JokboRowUI.cs](Sources/Assets/LuTestFolder/Scripts/UI/JokboRowUI.cs), [JokboScrollClickTarget.cs](Sources/Assets/LuTestFolder/Scripts/UI/JokboScrollClickTarget.cs) |
| 족보 사용 횟수 | [Assets/LuTestFolder/Scripts/Manager/JokboPlayTracker.cs](Sources/Assets/LuTestFolder/Scripts/Manager/JokboPlayTracker.cs) |
| 전투 결과 | [Assets/LuTestFolder/Scripts/UI/BattleResultUI.cs](Sources/Assets/LuTestFolder/Scripts/UI/BattleResultUI.cs), [BattleResultPanelView.cs](Sources/Assets/LuTestFolder/Scripts/UI/BattleResultPanelView.cs) |
| 게임 종료 결과 | [Assets/LuTestFolder/Scripts/Manager/GameResultUIManager.cs](Sources/Assets/LuTestFolder/Scripts/Manager/GameResultUIManager.cs) |
| 튜토리얼 | [Assets/Scripts/Tutorial/TutorialDirector.cs](Sources/Assets/Scripts/Tutorial/TutorialDirector.cs) |

## 자료 공개 범위

관련 C# 코드와 설명을 제공합니다. 코드 본문은 복사 시점 원본 그대로이며, 팀원·AI가 추가한 부분을 임의로 제거하거나 본인 초기 버전으로 표현하지 않았습니다.

- SO 7개, 매니저 6개, UI·결과 13개, 튜토리얼 1개, 보스 디버프 21개, 도감 6개를 포함합니다.
- Hot Reload, MagicaCloth2, DOTween Pro 등 외부 패키지 본체, 이미지·모델·음원·폰트, prefab·scene·SO 에셋, 원본 Git 이력과 백업 ZIP은 포함하지 않습니다.
- 일부 스크립트는 외부 API와 이 저장소에 없는 프로젝트 타입을 참조합니다. **코드 열람용 자료이며, 이 저장소만으로 컴파일하거나 게임을 실행할 수 없습니다.** 이 묶음에 대한 별도 컴파일·실행 검증은 하지 않았습니다.
- 원본 팀 프로젝트의 공개 설정과 파일은 변경하지 않았습니다.

<!--
자료 추가 시 참고:
- 데이터 구성 화면: 제작에 참여한 카드·부적·조커 등의 SO Inspector와 설정값
- 대표 플레이 화면: 카드 제출과 전투 진행을 볼 수 있는 장면
- 옵션 영상: 열기, 메뉴 이동, ESC, 게임 조작 복귀
- 족보 화면: 칩, 배수, 사용 횟수가 함께 보이는 장면
- 결과 화면: 보상 행 표시와 게임 종료 통계를 보여주는 장면
- 튜토리얼 영상: 강조 대상과 구매/리롤 완료에 따른 안내 전환
이미지 파일을 실제로 추가한 뒤 Markdown 이미지 링크를 작성합니다.
담당 범위와 기본 틀 작성 이후의 변경 사항은 게시 전에 본인이 최종 확인합니다.
-->
