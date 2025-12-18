# Release Command

버전 $ARGUMENTS 으로 릴리스를 수행합니다.

## 수행할 작업

1. **update.xml 버전 업데이트**: `<version>` 태그와 다운로드 URL의 버전을 $ARGUMENTS 로 변경
2. **TarkovHelper.csproj 버전 업데이트**: `<Version>`, `<AssemblyVersion>`, `<FileVersion>` 모두 $ARGUMENTS 로 변경
3. **Release 빌드**: `dotnet build TarkovHelper/TarkovHelper.csproj -c Release`
4. **ZIP 파일 생성**: PowerShell로 `TarkovHelper\bin\Release\CreateRelease.bat` 실행
5. **Git 커밋 및 태그**:
   - `git add -A`
   - `git commit -m "v$ARGUMENTS Release"`
   - `git tag v$ARGUMENTS`
6. **Push**: `git push origin main && git push origin v$ARGUMENTS`
7. **Release Notes 생성 및 GitHub Release 생성**:
   - 이전 태그 찾기: `git describe --tags --abbrev=0 v$ARGUMENTS^` (또는 태그 목록에서 이전 버전 확인)
   - 이전 태그와 현재 태그 사이의 커밋 로그 확인: `git log [이전태그]..v$ARGUMENTS --oneline`
   - 커밋 메시지를 분석하여 주요 변경 사항을 영어/한국어로 정리
   - gh CLI로 Release 생성 (전체 경로 사용: `C:\Program Files\GitHub CLI\gh.exe`)

## Release Notes 형식

```markdown
## What's Changed / 변경 사항

### English
- [Feature/Fix/Update description]
- ...

### 한국어
- [기능/수정/업데이트 설명]
- ...

---
**Full Changelog**: https://github.com/Zeliper/Tarkov-Item-Helper/compare/[이전태그]...v$ARGUMENTS
```

## Release Notes 작성 가이드

커밋 메시지 패턴에 따른 분류:
- `feat:` → New feature / 새로운 기능
- `fix:` → Bug fix / 버그 수정
- `DB Update` → Database update / 데이터베이스 업데이트
- `refactor:` → Code refactoring / 코드 리팩토링
- `chore:` → Maintenance / 유지보수 (일반적으로 Release Notes에서 생략)
- `Merge PR` → PR 제목에서 기능 추출

## 주의사항

- gh CLI가 설치되어 있고 인증되어 있어야 합니다 (`gh auth login`)
- gh CLI 경로: `C:\Program Files\GitHub CLI\gh.exe` (PATH에 없을 수 있음)
- 빌드 실패 시 중단합니다
- PowerShell로 bat 파일 실행: `powershell.exe -Command "cd '[경로]'; .\CreateRelease.bat"`
- **update.xml 수정 시 XML 태그가 누락되지 않도록 주의** (예: `</url>`, `</version>` 등 닫는 태그 필수)

위 작업들을 순서대로 실행해주세요. 각 단계마다 결과를 확인하고 진행하세요.
