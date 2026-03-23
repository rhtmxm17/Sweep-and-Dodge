# 05_Content
> 재배포 불가능한 콘텐츠 자산 보관 경로

## 용도
이 디렉토리는 프로젝트에서 사용하는 비공개 콘텐츠 자산을 보관한다.
공개 저장소는 코드, 구조, 작업 방식을 공유하는 것을 목적으로 하므로 이 경로의 자산은 포함하지 않는다.

## 범위
이 경로에는 이미지, 오디오, VFX 등 재배포 불가능한 자산을 둔다.
별도 명시가 없는 한 하위 자산은 모두 비공개로 간주한다.

## 폴더 규칙
콘텐츠는 도메인 기준으로 나누고, 그 아래에서 타입별로 정리한다.

예시:
```text
Assets/_Project/05_Content/
  UI/
    Images/
    Audio/
    VFX/
  Player/
    Images/
    Audio/
    VFX/
  Bullets/
    Images/
    Audio/
    VFX/
  Environment/
    Images/
    Audio/
    VFX/
  Shared/
    Images/
    Audio/
    VFX/
  90_Temp/
```

## 운영 규칙
- 공용 자산만 `Shared/`에 둔다.
- 임시 작업물은 `90_Temp/`를 사용하고 주기적으로 정리한다.
- 공개용 대체 자산이나 placeholder가 필요하면 이 경로 밖에 둔다.

## 참고
공개 환경에서 이 경로의 자산이 누락되는 것은 정상이다.
