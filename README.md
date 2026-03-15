# Minecraft Demo

**고2 때 만든 프로젝트라 가볍게 봐주세요. 현재는 유지보수하지 않습니다.**

이 프로젝트는 `Unity 2021.3.8f1c1`로 제작되었습니다.

**아래 이미지는 모두 실제 인게임 화면입니다.**


![인게임 스크린샷(1920x1080)](/Recordings/Capture.png)


![생물군계 경계(1920x1080)](/Recordings/biome.png)


![클래식 박스형 집(1920x1080)](/Recordings/house.png)


![박스형 집 내부(1920x1080)](/Recordings/house_inside.png)



# Features

* 무한 월드, 랜덤 지형
* 생물군계(사바나, 사막, 숲, 해변, MORE+)
* 동굴, 광맥(다이아몬드, 석탄, 금, MORE+)
* 조명, 앰비언트 오클루전, 그림자
* 글로우스톤, 횃불
* 흔들리는 나뭇잎
* 나무
* 반 블록(아직 병합 미지원)
* 탄성 충돌을 지원하는 슬라임 블록(이 프로젝트는 Unity 내장 물리 시스템 대신 간단한 자체 구현 사용)
* 폭발 가능한 TNT(파티클 효과 포함)
* 중력 영향을 받는 모래와 자갈
* 흐르는 물과 용암(최대 유동 칸 수 제한이 있어 물 한 칸이 월드를 전부 잠기게 하지는 않음)
* 블록 방향(회전)
* 낮/밤 순환
* URP 렌더 파이프라인 + 전체 PBR 머티리얼([NVIDIA의 Minecraft PBR 텍스처 팩 지원](https://www.nvidia.cn/geforce/guides/minecraft-rtx-texturing-guide/))
* 포스트 프로세싱 효과
* 커스텀 가능한 리소스 팩
* 블록/생물군계/아이템 데이터 고도 설정 가능
* 몇 가지 ~~버그~~ 특징



# Notice

* 프로젝트에서 사용한 텍스처 팩은 [“마인크래프트(Minecraft)” Windows 10 RTX Beta: 물리 기반 렌더링 텍스처 Q&A 및 무료 리소스 팩 다운로드 (nvidia.cn)](https://www.nvidia.cn/geforce/news/minecraft-with-rtx-beta-your-pbr-questions-answered/)에서 받은 `RTX Vanilla Conversion`이며, 제작자는 u/TheCivilHulk입니다. 또는 [TheCivilHulk/Minecraft-RTX-Vanilla-Conversion-and-Patches (github.com)](https://github.com/TheCivilHulk/Minecraft-RTX-Vanilla-Conversion-and-Patches)에서 직접 받을 수 있습니다.
* 비정기적으로 업데이트합니다!



# Guide

## How To Play In Unity?

1. SinglePlayer 씬을 엽니다
2. 재생 버튼을 클릭합니다
3. 화면의 키 안내를 따라 플레이합니다

## Asset Management

에디터에서는 두 가지 리소스 로드 모드를 지원합니다:

* `AssetDatabase`에서 로드(개발용 권장, `AssetBundle` 빌드 불필요, 리소스 수정 즉시 반영)

* `AssetBundle` 파일에서 로드(테스트용 권장, `AssetBundle` 빌드 필요)

두 모드는 Unity 메뉴바 `Minecraft-Unity/Assets/Load Mode`에서 전환하면 됩니다. 동일한 API를 공유하므로 상위 로직은 신경 쓸 필요가 없습니다.

코드 예시:

```c#
// 먼저 씬에 AssetManagerUpdater 컴포넌트가 붙어 있고 관련 속성이 설정되어 있는지, 또는 AssetManager를 수동 초기화했는지 확인하세요.
using Minecraft.Assets; // 관련 API는 모두 이 네임스페이스에 있습니다.

class Example : MonoBehaviour
{
    // 리소스 경로를 직접 저장합니다. 예: Assets/MyFolder/MyAsset.asset.
    public string AssetPath;

    // 리소스 "주소"를 저장합니다(권장).
    // Inspector에서는 일반 Object 참조와 차이가 없습니다.
    // 내부적으로 GUID를 저장하므로 리소스 위치/이름이 바뀌어도 참조가 유지됩니다!
    public AssetPtr Asset;

    private IEnumerator Start()
    {
        // 완전 비동기 구조입니다.
        // 리소스가 어떤 AssetBundle에 있는지 신경 쓸 필요가 없습니다.
        AsyncAsset asset = AssetManager.Instance.LoadAsset<GameObject>(Asset); // AssetPath를 사용해도 됩니다.
        yield return asset; // 리소스 로드 완료까지 대기.

        GameObject go = asset.GetAssetAs<GameObject>(); // 실제 객체를 가져옵니다.

        // do something.

        AssetManager.Instance.Unload(asset); // 리소스 언로드(여기서는 리소스 이름으로도 언로드 가능)
    }
}
```

위 예시는 가장 기본적인 사용법입니다. 더 많은 API는 소스 코드를 참고하세요(이 문서의 핵심은 아닙니다).

`AssetBundle` 빌드: Unity 메뉴바에서 `Minecraft-Unity/Assets/Build AssetBundles`를 클릭하면 현재 배포 플랫폼용 리소스 팩으로 자동 패키징되어 지정 경로에 저장됩니다.



## Configuration

블록 데이터, 아이템 데이터(현재는 미사용), 생물군계 데이터는 모두 커스텀 에디터에서 설정합니다.

Unity 메뉴바의 `Minecraft-Unity/MC Config Editor`를 클릭하면 열 수 있으며, 화면은 아래와 같습니다:



![에디터-블록](EditorRecordings/mc_config_editor_block.png)



![에디터-아이템](EditorRecordings/mc_config_editor_item.png)



![에디터-생물군계](EditorRecordings/mc_config_editor_biome.png)



### Tips

1. 툴바 왼쪽 드롭다운에서 현재 편집 데이터(Blocks / Items / Biomes)를 전환할 수 있습니다
2. 툴바의 + / - 버튼으로 데이터 항목(Block / Item)을 추가/삭제할 수 있습니다
3. 툴바 오른쪽은 왼쪽부터 검색창, 저장 버튼, 설정 버튼 순서입니다
4. 설정 버튼에서 설정 파일 저장 경로를 바꿀 수 있으니 처음 사용할 때 먼저 경로를 지정하세요
5. 제가 만든 설정 파일은 모두 `Assets/Minecraft Default PBR Resources/Tables/Configs`에 있습니다
6. 새로 구성하고 싶지 않다면 에디터를 연 직후 5번의 경로로 설정하세요
7. 저장되는 설정 파일이 꽤 많으며, 파일명 앞에 `[editor]`가 붙은 것은 Unity Editor 전용입니다
8. 7번에서 언급한 파일의 용도가 궁금하다면 소스 코드를 확인해주세요. 여기엔 다 적기 어렵습니다(페르마식 생략).
9. 추가로 궁금한 점이 있으면 이슈로 남겨주세요



## Lua Codes

Lua 코드는 `Assets/Minecraft Default PBR Resources/Lua Scripts` 경로에 있습니다. `main.lua`는 Lua 코드의 엔트리 포인트이며, `cleanup.lua`는 리소스 해제용입니다. 이 설정을 바꾸려면 Hierarchy에서 `Lua Manager`를 찾아 수정하세요.

`Minecraft.Lua` 네임스페이스에는 `ILuaCallCSharp`, `ICSharpCallLua`, `IHotfixable` 같은 마커 인터페이스가 제공됩니다. xlua에 익숙하다면 바로 이해할 수 있습니다. 예를 들어 `ILuaCallCSharp`를 구현하면 클래스에 `XLua.LuaCallCSharpAttribute`를 붙인 것과 같습니다.



### Write Custom Block Behaviours

먼저 Lua 파일을 만들고 AssetBundleName을 설정합니다.

다음으로 모래를 예로 들어보겠습니다:

```lua
require "block" -- 이 모듈을 가져오면 블록 동작 API를 사용할 수 있습니다
local gravity = require "blocks.templates.gravity" -- 제공된 동작 템플릿 중 중력 템플릿을 사용합니다
sand = create_block_behaviour(gravity) -- 바로 상속해서 완료
-- 주의: sand 변수는 반드시 전역 변수여야 하며, 에디터에서 설정한 이름과 일치해야 합니다
```

작성 후 `main.lua`에서 `require`하는 것을 잊지 마세요. 마지막으로 `block` 모듈의 간단한 소스 코드를 보겠습니다:

```lua
-- 파일 경로: Assets/Minecraft Default PBR Resources/Lua Scripts/block.lua

local behaviour = {} -- 기본 동작

-- 블록 동작이 등록될 때 호출
function behaviour:init(world, block)
    self.world = world
    self.__block = block
    print("init block behaviour: " .. block.InternalName)
end

-- 인접 블록이 갱신될 때 호출
function behaviour:tick(x, y, z)
    -- default implement
end

-- 블록이 배치된 뒤 호출
function behaviour:place(x, y, z)
    -- default implement
end

-- 블록이 파괴된 뒤 호출
function behaviour:destroy(x, y, z)
    -- default implement
end

-- 블록이 클릭될 때 호출
function behaviour:click(x, y, z)
    -- default implement
end

-- 블록 엔티티 초기화 시 호출
-- entity 파라미터는 블록 엔티티 객체
-- context 파라미터는 LuaTable이며 임시 데이터 저장에 사용(직접 self.xxx = xxx 사용 금지)
-- 이하 동일
function behaviour:entity_init(entity, context)
    -- default implement
end

-- 블록 엔티티가 파괴될 때 호출
function behaviour:entity_destroy(entity, context) 
    -- default implement
end

-- 블록 엔티티의 Update 메서드
function behaviour:entity_update(entity, context)
    -- default implement
end

-- 블록 엔티티의 FixedUpdate 메서드
function behaviour:entity_fixed_update(entity, context)
    -- default implement
end

-- 블록 엔티티가 충돌할 때 호출
-- flags 파라미터는 UnityEngine.CollisionFlags
function behaviour:entity_on_collisions(entity, flags, context)
    -- default implement
end

-- 해당 동작에 대응하는 BlockData 객체 가져오기
function behaviour:get_block_data()
    return self.__block
end

--- 블록 동작 객체를 생성합니다.
---
--- `base` 파라미터를 전달하지 않으면 기본 동작을 갖는 객체를 반환합니다.
---
--- `base` 파라미터를 전달하면 반환 객체가 `base`의 모든 동작을 상속합니다.
---
--- @param base? table
--- @return table
function create_block_behaviour(base)
    return setmetatable({
        base = base or behaviour
    }, {
        __index = function(table, key)
            local block = rawget(table, "__block")
            return block and block[key] or rawget(table, "base")[key]
        end
    })
end
```

이 모듈을 사용하면 클래스를 정의하듯 블록 동작을 정의할 수 있어 매우 편리합니다. 또한 `blocks.templates.gravity`, `blocks.templates.fluid` 두 가지 템플릿을 제공해 중력/유체 동작을 빠르게 구현할 수 있습니다. 더 복잡한 동작은 직접 작성해야 합니다.



## Terrain Generation

`Assets/Minecraft Default PBR Resources/WorldGen` 경로에는 지형 생성 관련 설정 파일이 다수 있으며, 파라미터를 수정하거나 생성기를 추가해 지형을 변경할 수 있습니다.



# End

질문이 더 있으면 이슈를 남겨주세요. 확인 후 답변드리겠습니다.



# References

**순서는 무작위입니다**

* [TrueCraft](https://github.com/ddevault/TrueCraft)
* [xLua](https://github.com/Tencent/xLua)
* [MineCase](https://github.com/dotnetGame/MineCase)
* [MineClone-Unity](https://github.com/bodhid/MineClone-Unity)
* [MinecraftClone](https://github.com/Shedelbower/MinecraftClone)
* [Making a Minecraft Clone](https://www.shedelbower.dev/projects/minecraft_clone/)
* [Minecraft_Wiki](https://minecraft-zh.gamepedia.com/Minecraft_Wiki)
* [초지하이커셰관쉬의 CSDN 블로그](https://blog.csdn.net/xfgryujk)

