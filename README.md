<div align="center">

# ⚾ CS-Baseball-Mod

### 시티즈: 스카이라인 · 프로야구 리그 모드

**도시에 프로야구 리그를 엽니다.**<br>
게임에 이미 있는 경기장 경기 이벤트를 가상 팀 리그의 한 시즌으로 묶고, 일정 · 순위 · 기록 · 포스트시즌까지 이어 갑니다.

![Cities: Skylines](https://img.shields.io/badge/Cities%3A%20Skylines-1.21-1F6FEB?style=for-the-badge)
![Version](https://img.shields.io/badge/판-1.0.0-3F9A52?style=for-the-badge)
![Status](https://img.shields.io/badge/상태-게임%20안%20확인%20전-8A4A00?style=for-the-badge)

![.NET Framework 3.5](https://img.shields.io/badge/.NET%20Framework-3.5-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Harmony 2](https://img.shields.io/badge/Harmony-2.x-7F8894?style=flat-square)
![CSCommon](https://img.shields.io/badge/UX-CSCommonUX%203.0-2ED4BF?style=flat-square)
![Language](https://img.shields.io/badge/언어-한국어-5CC8FF?style=flat-square)

[무엇을 하나요](#-무엇을-하나요) · [리그 규칙](#-리그-규칙) · [화면](#-화면) · [게임에 닿는 곳](#-게임에-닿는-곳) · [빌드](#-빌드) · [진행 상황](#-진행-상황) · [이용 허락](#-이용-허락)

</div>

> [!IMPORTANT]
> 이 저장소는 공개되어 있지만 오픈소스 라이선스가 없습니다. 코드를 복사 · 수정 · 재배포하려면 **원저작자의 허락**을 먼저 받아야 합니다. 자세한 내용은 [이용 허락](#-이용-허락)을 보세요.

---

## 🏟️ 무엇을 하나요

| | 기능 | 내용 |
|:-:|---|---|
| ⚾ | **리그 · 시즌** | 내 도시 팀 1팀 + 가상 원정 팀으로 리그를 만들고 시즌을 돌림 · 순위 · 경기 차 · 포스트시즌 |
| 🏠 | **홈 경기** | 도시 경기장의 경기 이벤트(Match Day · Campus)를 홈 경기로 기록 · 승패는 게임이 정하고 모드는 팀 전력으로 승률만 조정 · 점수 · 선수 기록은 그 승패에 맞춘 시뮬레이션 |
| ✈️ | **원정 경기** | 도시 밖 경기는 모드가 타석 단위 시뮬레이션으로 정함 · 경기장이 없는 도시는 원정만 진행 |
| 📅 | **일정** | 리그 규칙(KBO · NPB)대로 시즌 일정을 만듦 · 우천 취소는 예비일로 다시 잡음 |
| 🧾 | **경기 상세** | 진행 중인 경기만 열림(시작 시각부터 게임 시간 3시간) · 점수판 · S/B/O · 주자 · 이닝 점수 · 이번 경기 기록 · 상대 전적 · 문자 중계 |
| 🏟️ | **관중** | 홈구장 경기는 게임이 센 방문객, 나머지는 자리 · 순위 · 상대 · 요일로 추정 · 홈 경기 관중 · 평균 · 최다 · 만원 · 예상 관중 |
| 👥 | **팀 · 선수** | 팀 기록 · 최근 10경기 · 팀 전력 · 타순 · 투수 · 홈팀 이름 · 색 · 홈구장 설정 |
| 🏆 | **기록** | 선수 시즌 기록(타율 · 홈런 · 타점 · 평균자책 · 승 · 세이브 · 탈삼진) · 시즌 리더 · 승률 추이 · 시즌 MVP · 포스트시즌 경쟁 · 우승 이력 |
| 🧭 | **창 열기** | 모드 허브(Yuki Mod Box)가 있으면 허브 목록에서 열고, 허브가 없거나 설정에서 '모드 허브 연결'을 끄면 화면 왼쪽 퀵 버튼으로 엶(끌어서 옮기기 · 자리 기억) |
| 🔔 | **알림** | Chirper 대신 모드 알림 카드 — 경기 전 · 경기 결과 · 큰 소식(진출 · 우승 · MVP) · LIVE 점수판 |

실제 구단 이름 · 로고 · 닉네임은 쓰지 않습니다. 팀 이름은 **한국형**(가상 모기업 + 닉네임) · **일본형** 중에서 고릅니다.

---

## 📋 리그 규칙

설정에서 리그 규칙을 고릅니다. **시즌이 진행 중이면 리그 규칙 · 시즌 · 일정 규칙은 잠기고**, 시즌이 끝나거나 새 시즌을 시작할 때 바꿀 수 있습니다.

| 항목 | KBO (한국) | NPB (일본) |
|---|---|---|
| 리그 | 한 리그 10팀 | 센트럴 6팀 · 퍼시픽 6팀 |
| 정규 시즌 | 팀당 144경기 · 상대마다 16경기 | 팀당 143경기 · 같은 리그 125 + 교류전 18 |
| 일정 | 월요일 휴식 · 화~목 / 금~일 3연전 | 월요일 휴식 · 3연전 · 5~6월 교류전 |
| 시작 시간 | 평일 18:30 · 주말 17:00 | 평일 18:00 · 주말 14:00 |
| 포스트시즌 | 5팀 · 와일드카드 → 준PO → PO → 한국시리즈 | 리그별 3팀 · 클라이맥스 시리즈 → 일본시리즈 |
| 우천 취소 | 예비일 · 더블헤더 | 예비일 |

> 시작 시간과 단기전 세부 방식은 공식 규정과 다시 대조할 예정입니다.

---

## 🖥️ 화면

창은 CSCommonUX 3.0 라이트 모드 · 하단 알약 툴바 + 설정 버튼입니다.

| 탭 | 메인 화면 | 상세 화면 (진입 경로) |
|---|---|---|
| **홈** | 오늘 경기 · 순위 · 최근 경기 · 다음 경기 · 월별 관중 | 홈구장 (빠른 실행 › 홈구장) |
| **일정** | 월간 달력 · 오늘 리그 경기 · 이번 주 3연전 | 경기 상세 (오늘 리그 경기 › 진행 중인 경기) |
| **팀** | 팀 목록 · 팀 기록 · 팀 전력 · 타순 · 투수 | 홈팀 설정 (내 도시 팀 카드 › 홈팀 설정) |
| **기록** | 시즌 리더 · 승률 추이 · 포스트시즌 경쟁 · 우승 이력 | — |
| **설정** | 리그 규칙 · 시즌 · 일정 규칙 · 경기 · 알림 · 상태 진단 | 알림 카드 시안 |

상세 화면은 하단 툴바 왼쪽 뒤로가기 버튼으로 돌아갑니다.

---

## 🔌 게임에 닿는 곳

패치 대상은 게임 어셈블리(`Assembly-CSharp.dll`)에서 확인한 이름이고, Harmony 패치는 홈구장 경기에만 값을 바꿉니다.

| 하는 일 | CS1 API |
|---|---|
| 경기장 경기 이벤트 | `EventManager` · `EventData` · `PlayerBuildingAI.m_supportEvents`(`Football` · `VarsitySportsMatch`) 로 홈구장 후보를 찾음 |
| 경기 결과 | `SportMatchAI.EndEvent` 패치(Prefix · Postfix) — 홈구장 경기가 끝나면 `EventData.Flags` 의 `Success` · `Failure` 와 방문객 수를 읽어 홈 경기 결과로 넣음(대학 경기장은 `VarsitySportsMatchAI` 가 base 로 지남) |
| 홈 팀 승률 조정 | `SportMatchAI.GetWinProbability` · `VarsitySportsMatchAI.GetWinProbability` (0 ~ 100) |
| 대학 경기장 | `VarsitySportsArenaAI` (Campus) |
| 경기장 정보 창 팀 이름 | `FootballPanel.RefreshMatchInfo` · `VarsitySportsArenaPanel.RefreshMatchInfo` 패치(Postfix) — 홈구장 창이면 게임이 `SportMatchAI.GetTeamName` · `GetOpponentName` 으로 채운 팀 이름 · 상대 · 지난 경기 툴팁 글과 팀 색만 리그 값으로 덮음(게임 값 · 세이브는 그대로) · 올림픽 모드가 붙인 경기(`OlympicGames.Logic.MatchDay.IsAttached`, 리플렉션)는 그대로 |
| 게임 날짜 | `SimulationManager.m_currentGameTime` |
| 경기장 상태 · 지금 입장 | `Building.m_problems` · 건물의 `CitizenUnit`(Visit) 방문 시민 수 |
| 홈구장 카메라 이동 | `ToolsModifierControl.cameraController.SetTarget` |
| 시즌 데이터 저장 | `ISerializableDataExtension` |
| 올림픽 모드와 함께 쓸 때(선택) | `CS-Olympic-Mod` 의 `OlympicGames.Logic.Runner.Phase` · `ArrivalAt` · `OpenAt` · `CloseAt` 를 리플렉션으로 하루 한 번 읽어, 대회가 확정되면 선수단 입촌 ~ 폐회 동안 리그를 쉬고 그 기간 경기를 예비일로 옮김 · 모드가 없으면 그대로 |

필요한 DLC: **Match Day**(축구 경기장 · 무료). Campus가 있으면 대학 경기장도 홈구장으로 쓸 수 있습니다.

---

## 🔧 빌드

```bash
git clone --recurse-submodules https://github.com/chbk1348/CS-Baseball-Mod.git
dotnet build CS-Baseball-Mod/BaseballLeague -c Release
```

- 공통 UI 는 git 서브모듈 `CSCommonUX/`(정본 저장소 [chbk1348/CSCommonUX](https://github.com/chbk1348/CSCommonUX))입니다. 서브모듈 없이 받았다면 `git submodule update --init` 으로 받습니다.
- 게임 DLL 경로가 다르면 `-p:ManagedDir="...\Cities_Skylines\Cities_Data\Managed"`
- 빌드하면 `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\CS-Baseball-Mod` 로 복사됩니다.
- Steam 창작마당 **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402)** 가 필요합니다.

---

## 🚧 진행 상황

- [x] 후보 기획 · 단독 모드 확인
- [x] 디자인 시안 (CSCommonUX 3.0 · 탭 4개 + 설정 + 상세 화면 3개 + 알림 카드)
- [x] 프로젝트 구성 · CSCommonUX 서브모듈
- [x] 리그 · 일정 생성 (KBO · NPB)
- [x] 경기 이벤트 연결 · 승률 조정
- [x] 포스트시즌 · 더블헤더 · 우승 이력
- [x] 화면 구현 (탭 4개 + 설정 · 홈구장 · 홈팀 설정)
- [x] 선수 명단 · 경기 시뮬레이션(타석 단위 · 결정적) · 박스 스코어 · 선수 시즌 기록
- [x] 경기 상세(진행 중 경기) · 시즌 리더 · 승률 추이 · MVP · 관중 · 타순 · 투수 · 홈팀 편집
- [x] 알림 카드(경기 전 · 결과 · 큰 소식 · LIVE 점수판)
- [x] 시즌 저장(형식 판 4 · 옛 세이브 읽기) · 허브 없을 때 퀵 버튼
- [x] 올림픽 모드 연동(입촌 ~ 폐회 휴식)
- [ ] 게임 안 확인 · 균형 조정

---

## 📜 이용 허락

이 모드의 저작권은 원저작자([@chbk1348](https://github.com/chbk1348))에게 있고, 별도의 오픈소스 라이선스를 붙이지 않았습니다. 저장소가 공개되어 있어도 아래 일을 하려면 **원저작자의 허락을 먼저 받아야 합니다.**

- 코드 · 리소스의 전부나 일부를 복사하거나 다른 프로젝트에 넣기
- 고친 판(포크 · 변형판)을 만들어 배포하기
- Steam 창작마당 등 다른 곳에 올리거나 다시 배포하기
- 상업적으로 쓰기

허락 없이 할 수 있는 일:

- 저장소를 둘러보고 코드를 읽기
- 개인 용도로 빌드해 자기 게임에서 쓰기
- 이슈로 버그 · 제안 알리기

허락 요청은 이 저장소의 [이슈](https://github.com/chbk1348/CS-Baseball-Mod/issues)로 남겨 주세요. 공통 UI 서브모듈 `CSCommonUX` 도 같은 조건을 따릅니다.
