# 보스전 구현 및 마무리 기록

마감 목표: 2026년 11월 30일. 이 문서는 코드 조사 결과와 검증 계획을 구분해 기록한다. 실제 플레이와 프로파일링으로 확인하지 않은 내용은 완료로 표시하지 않는다.

## 현재 코드의 책임과 실행 흐름

| 소유자 | 클래스 | 책임 |
|---|---|---|
| Boss/Root | `BossRoot` | 체력·전투·이동·View의 참조 검사, Bind 순서, 사망 이벤트 연결 |
| Boss/Action | `BossActionStateFlow` | Dormant, Intro, Combat, PhaseTransition, Dead 상태와 현재 페이즈 |
| Boss/Combat | `BossIdleState` | 대상과 공격 후보 선택. 사거리, 최근 시전 순서, 방해 공격 이력 고려 |
| Boss/Combat | `BossCombatFlow` | 추적·공격·회복 전환, 반복 시전과 Attack7의 복합 단계 판단 |
| Boss/Combat | `BossCombatModule` | 대상 물리 조회, 공격 객체 생성, 지면 투영, 소환 위치 계산 |
| Boss/Combat | `BossAttackPattern` | 패턴별 선택 조건, 이동과 생성 설정 |
| Boss/Combat | `BossAttackInstance` / `BossAttackAreaModule` | 판정 구간과 대상 조회, 피격 전달 |
| Boss/Locomotion | `BossLocomotionStateFlow` / `BossLocomotionModule` | 추적 상태 판단 / 이동 요청 |
| Boss/View | `BossAnimationView` / `BossAttackEffectView` | 상태와 애니메이션 이벤트의 표현, 이펙트 생성 |

```text
BossRoot.Bind
  → BossActionStateFlow의 Combat 상태
  → BossIdleState가 대상·패턴 선택
  → BossCombatFlow가 추적·공격·회복 결정
  → BossCombatModule과 Locomotion이 실행
  → BossAnimationView가 상태를 표시하고 애니메이션 이벤트를 전달
  → BossAttackInstance가 판정하고 Player에 피격 요청
```

`BossRoot`는 Attack1/4/5의 특수 객체를 직접 찾아 Bind한다. 이 연결은 현재 프리팹 구성과 함께 검증해야 하므로, 구조를 바꿀 때는 Scene/Prefab의 MonoBehaviour 참조부터 확인한다. Attack7은 Attack3의 점프 클립을 재사용하고 복귀 지점과 다음 공격 단계를 `BossCombatFlow`에서 결정한다.

## 코드 조사에서 확인한 개선 지점

1. `BossAttackAreaModule.OverlapTargets`는 공격 판정 조회마다 새 `Collider[]`를 만들었다. 재사용 버퍼와 버퍼 포화 시 전체 조회를 사용해 통상 경로의 할당을 줄였다. **실제 GC Alloc 감소량은 아직 측정 전이다.**
2. `Projectile.OnImpact`의 범위 조회와 이펙트 생성, `BossAttackEffectView`의 생성/삭제는 전투 부하를 측정할 후보이다. 호출 수와 GC Alloc을 먼저 확인한 뒤 풀링 여부를 결정한다.
3. `BossActionStateFlow`에는 페이즈 전환 메서드가 있으나 호출하는 코드가 없다. 페이즈 콘텐츠를 만들려면 체력 임계값, 공격 종료와 전환 시점, 전환 연출, 패턴 선택 범위를 함께 결정해야 한다.
4. README는 프로젝트 이름만 있었다. 이번 작업부터 플레이 방법, 직접 구현한 흐름, 문제 해결과 성능 수치를 누적한다.

### 11/8 변경 검증

- `BossAttackAreaModule`의 박스·원형 조회를 재사용 버퍼로 바꾸고, 32개 이상 감지되면 기존 전체 조회로 전환했다. 두 호출자(`BossAttackInstance`, `BossAttack5ContactModule`)는 반환된 후보 수만 순회한다.
- Unity 6000.0.54f1 배치 로그에서 스크립트 컴파일 단계가 완료되고 C# 컴파일 오류는 기록되지 않았다. 배치 프로세스의 종료 코드는 비정상적으로 보고되어 Play 모드 동작과 성능 개선 수치는 아직 검증되지 않았다.
- 변경 전후 Profiler 수치가 없으므로 성능 향상률은 기재하지 않는다.

## 11월 작업 순서

| 기간 | 작업 | 완료 기준 |
|---|---|---|
| 11/8–11/12 | 보스전 전 구간 플레이, 예외·누락 참조·피격/사망 오류 기록, 기준 성능 캡처 | 재현 절차와 기준 수치가 기록됨 |
| 11/13–11/19 | 전투 콘텐츠 한 가지 완성: 페이즈 변화 또는 시작·종료/재도전 흐름 중 플레이에서 가장 큰 빈틈 선택 | 시작부터 종료까지 막힘 없이 플레이 가능 |
| 11/20–11/25 | Profiler 결과에 근거한 최적화와 회귀 확인 | 같은 조건에서 변경 전후 수치와 판정 결과 비교 |
| 11/26–11/30 | 빌드, 플레이 영상, 코드·설계·기여도·검증 기록 정리 | 다른 사람이 실행과 기술적 선택을 이해할 수 있음 |

콘텐츠 범위를 늘리기 전에 실제 플레이에서 가장 약한 구간을 확인한다. 이미 공격 패턴이 여러 개 있으므로 새 패턴의 수보다 전투 시작과 끝, 피격 전달, 페이즈 변화의 완성도를 우선한다.

## 플레이 및 성능 검증 기록

동일한 씬, 품질 설정, 해상도, 보스 패턴, 측정 구간에서 비교한다. 에디터 수치와 빌드 수치를 혼용하지 않는다. Unity Profiler의 CPU Usage와 Memory 모듈에서 `GC Alloc`, 프레임 시간, 물리 조회 구간을 확인하고, 필요하면 Profile Analyzer로 반복 구간을 비교한다.

| 항목 | 변경 전 | 변경 후 | 환경/측정 구간 |
|---|---:|---:|---|
| 프레임 시간 (평균/상위 구간) | 미측정 | 미측정 | |
| GC Alloc (B/frame) | 미측정 | 미측정 | |
| 보스 판정 조회 시간 | 미측정 | 미측정 | |
| 투사체·이펙트 동시 생성 시 프레임 시간 | 미측정 | 미측정 | |

### 회귀 확인 목록

- [ ] Attack1/4/5의 기본 판정과 쉴드 차단
- [ ] Attack7 점프, 타깃 재조준, 소환 단계
- [ ] 공격 중 대상 상실과 추적 실패 후 재선택
- [ ] 보스 사망 시 이동·공격 중단
- [ ] 많은 충돌체가 공격 범위에 겹쳐도 대상 누락 없음
- [ ] 전투 시작부터 승리/패배와 재도전까지 플레이

## 포트폴리오 제출 전 남길 근거

- 직접 작성한 클래스와 사용한 외부 에셋/패키지의 범위
- 가장 어려웠던 문제 2~3개의 재현 방법, 원인, 선택한 수정, 검증 결과
- 보스전 1~2분 플레이 영상과 실행 가능한 빌드
- 성능 개선 전후 동일 조건의 Profiler 캡처 및 수치
