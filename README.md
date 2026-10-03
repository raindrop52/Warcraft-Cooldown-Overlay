# Warcraft Cooldown Overlay

Warcraft III 프로즌 쓰론 창에서 인벤토리와 스킬 아이콘을 캡처해 별도의 쿨타임 오버레이로 보여주는 Windows 프로그램입니다.

## 기능

- 인벤토리 6칸 및 지정된 스킬 7칸 실시간 미리보기
- 표시할 아이템과 스킬 개별 선택
- 가로/세로 오버레이 배치
- 선택 상태, 창 위치 및 오버레이 위치 자동 저장
- `F7`: 오버레이 실행/종료
- `F8`: 프로그램 전체 종료

## 다운로드

[Releases](../../releases) 페이지에서 최신 `WarcraftCooldownOverlay.exe`를 내려받아 실행하세요. 별도의 .NET 설치가 필요하지 않습니다.

## 직접 빌드

Windows에서 .NET 8 SDK를 설치한 뒤 다음 명령을 실행합니다.

```powershell
dotnet build .\WarcraftCooldownOverlay\WarcraftCooldownOverlay.csproj
```

단일 실행 파일 게시:

```powershell
dotnet publish .\WarcraftCooldownOverlay\WarcraftCooldownOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 단축키와 설정

사용자 설정은 `%LocalAppData%\WarcraftCooldownOverlay\settings.json`에 저장됩니다.

