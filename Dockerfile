# Guildwright — 텍스트 프로토타입
#
# .NET SDK를 설치하지 않고 플레이하기 위한 이미지입니다.
#
#   docker build -t guildwright .
#   docker run -it --rm guildwright                 # 플레이
#   docker run -it --rm guildwright sim 400 5       # 배치 시뮬레이션
#   docker run -it --rm guildwright 12345           # 시드 지정
#
# ⚠️ 반드시 -it 를 붙이세요. 없으면 입력을 못 받아 첫 질문에서 바로 종료됩니다.

# ── 빌드 ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 프로젝트 파일만 먼저 복사해서 restore 계층을 캐시합니다.
# 소스만 고쳤을 때 패키지를 다시 받지 않게 하려는 것입니다.
COPY Guildwright.sln ./
COPY src/Guildwright.Core/Guildwright.Core.csproj src/Guildwright.Core/
COPY src/Guildwright.Console/Guildwright.Console.csproj src/Guildwright.Console/
COPY src/Guildwright.Web/Guildwright.Web.csproj src/Guildwright.Web/
COPY src/Guildwright.WebHost/Guildwright.WebHost.csproj src/Guildwright.WebHost/
COPY tests/Guildwright.Core.Tests/Guildwright.Core.Tests.csproj tests/Guildwright.Core.Tests/
RUN dotnet restore

COPY . .

# 테스트를 빌드 단계에서 돌립니다. 깨진 채로 이미지가 나가지 않게 하려는 것입니다.
RUN dotnet test --no-restore -v q

RUN dotnet publish src/Guildwright.Console -c Release -o /app --no-restore

# ── 실행 ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

# 화면이 전부 한국어라 UTF-8이 아니면 깨집니다.
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# 인자를 그대로 넘겨 sim / 시드 지정이 됩니다.
ENTRYPOINT ["dotnet", "Guildwright.Console.dll"]
