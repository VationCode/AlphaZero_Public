# MagicaCloth 배율 변경 전 설정

2026-10-21 기준의 `Silyon.prefab`과 `Herna.prefab`을 보존한다. `*.prefab.txt`는 바이트 단위 원본이고, `*Baseline.prefab`은 Unity 에디터 복원 도구가 읽는 사본이다. 백업 프리팹은 수정하지 않는다.

| 캐릭터 | MagicaCloth | Magica 콜라이더 | SHA-256 |
| --- | ---: | ---: | --- |
| Silyon | 3 | 3 | `855A3D2A77FE33C6F166DBFB0944764C82A8E5C3ADC3BB725857BA1F170C9B16` |
| Herna | 3 | 4 | `CB8F35A88A42A55EC80E932F06615C36C17479B137420AB9CCE6984521A7BA4B` |

FBX 배율을 변경한 뒤 `Tools > Character > Restore MagicaCloth Settings`에서 해당 캐릭터의 복원 버튼을 누른다. 화면에 보이는 캐릭터 크기가 그대로면 `New / old visible size`는 `1`이다. 도구는 현재 프리팹의 참조와 빌드 데이터를 보존하면서 클로스 설정 수치를 복원하고, 원래 100배였던 본을 기준으로 콜라이더의 위치와 크기를 보정한다. 리그 변경 뒤에는 MagicaCloth 빌드 데이터를 다시 생성해야 한다.
