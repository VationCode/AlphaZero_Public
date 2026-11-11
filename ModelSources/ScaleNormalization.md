# 캐릭터 FBX 배율 정규화

Anby와 Soldier11의 Blender 원본에서 리그와 메시 객체 Scale은 이미 `(1, 1, 1)`이었다. 기존 FBX 내보내기의 기본 배율 옵션은 FBX `UnitScaleFactor=1`과 리그·메시 노드 Scale `100`을 만들었다. Unity는 이를 `fileScale=0.01`로 읽었다.

FBX 내보내기에 `apply_scale_options="FBX_SCALE_UNITS"`를 사용하면 미터 단위가 FBX 메타데이터(`UnitScaleFactor=100`)에 기록되고 리그·메시 노드 Scale은 `1`이 된다. Unity에서는 `fileScale=1`로 읽으며 캐릭터의 월드 크기가 유지된다. `prepare_unity_fbx.py` 두 파일에 이 옵션과 현재 Asset 경로를 반영했다.

2026-10-21에 기존 FBX를 Blender 4.2로 다시 읽어 Anby 캐릭터, Anby 무기, Soldier11 캐릭터를 같은 이름과 계층으로 내보냈다. 기존/신규 FBX의 메시 정점 좌표, 폴리곤 인덱스, 재질·텍스처 이름, 리그 노드 개수가 같음을 확인했다. 분리된 Unity 6000.0.54f1 프로젝트에서 Anby/Soldier11 리그 Local Scale이 `100 → 1`, 캐릭터 렌더러 Bounds가 각각 동일한 것을 확인했다.

Anby의 Humanoid `humanDescription.skeleton`은 기존 FBX의 `fileScale=0.01`에 맞는 로컬 본 위치를 저장하고 있었다. 새 FBX에 맞춰 해당 위치 245개를 100배로 보정했다. 분리된 Unity 프로젝트에서 두 캐릭터의 Avatar가 유효한 Humanoid로 생성되고 Rig 오류가 없음을 확인했다. 프리팹에 추가된 MagicaCloth 콜라이더 7개는 본 배율 변경에 맞춰 Local Position과 Scale을 보정했다. Silyon 프리팹의 `Rightdummy_R` 본 위치 오버라이드도 같은 비율로 보정했다. 분리된 Unity 프로젝트에서 두 프리팹이 참조를 유지하며 로드되고, 7개 콜라이더의 월드 Scale이 모두 `(1, 1, 1)`인 것을 확인했다. MagicaCloth 설정 수치는 이전 백업을 유지한다.

원본 FBX는 Git 기록과 `Temp/UnityScaleProbe/Assets/*Old.fbx`에서 복구할 수 있다. 실제 게임 씬의 애니메이션과 MagicaCloth 시뮬레이션은 Unity 에디터에서 재확인해야 한다.
