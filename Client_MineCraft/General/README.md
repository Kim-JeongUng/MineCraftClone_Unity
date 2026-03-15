## xLua 범용 버전

xLua 범용 버전은 C# 환경에서 Lua 스크립트 지원을 제공하는 데 목적이 있습니다. Unity 버전과 비교해 print를 Console 창으로 리디렉션하는 기능, Unity 전용 스크립트 로더 등만 제거했고 나머지 기능은 모두 유지됩니다. 기능 목록은 [여기](../Assets/XLua/Doc/features.md)를 참고하세요.

## 사용 방법

XLua.Mini.dll을 프로젝트에 넣고, 해당 버전의 xlua 네이티브 동적 라이브러리를 pinvoke로 로드 가능한 경로(예: 실행 파일 디렉터리)에 배치하세요.

## 코드 생성 [선택]

XLua.Mini.dll은 리플렉션으로 Lua와 C# 간 상호작용을 처리합니다. 더 높은 성능이 필요하면 코드 생성을 사용할 수 있습니다.

1. [XLua의 설정.doc](../Assets/XLua/Doc/XLua%E7%9A%84%E9%85%8D%E7%BD%AE.doc) 문서에 따라 생성할 타입을 설정합니다.

2. 다시 컴파일한 뒤 도구 `XLuaGenerate`로 빌드 결과(exe 또는 dll)에 대해 코드 생성을 실행합니다: `XLuaGenerate xxx.exe/xxx.dll`. 생성 코드는 현재 디렉터리의 `Gen` 폴더에 저장됩니다.

3. 기존과 동일한 새 프로젝트를 만들고 `XLUA_GENERAL` 매크로를 추가합니다.

4. `XLua.Mini.dll`을 제거하고 XLua 소스 패키지(배포판의 `Src` 디렉터리)와 2단계에서 생성한 코드를 추가합니다.

5. 이제 이 프로젝트에서 생성되는 exe 또는 dll은 생성 코드에 맞게 적용됩니다.

## Hotfix

이미 코드 생성이 완료된 exe 또는 dll은 `XLuaHotfixInject` 도구로 주입하면 됩니다. Hotfix 기능 상세 사용법은 [Hotfix 가이드](../Assets/XLua/Doc/hotfix.md)를 참고하세요.


## 빠른 시작

~~~csharp

using XLua;

public class XLuaTest
{
    public static void Main()
    {
        LuaEnv luaenv = new LuaEnv();
        luaenv.DoString("CS.System.Console.WriteLine('hello world')");
        luaenv.Dispose();
    }
}

~~~


