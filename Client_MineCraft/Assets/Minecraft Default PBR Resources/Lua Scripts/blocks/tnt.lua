require "block"
local util = require "xlua.util"

tnt = create_block_behaviour()
local unityTime = CS.UnityEngine.Time
local unityColor = CS.UnityEngine.Color
local playerModification = CS.Minecraft.ModificationSource.PlayerAction
local systemModification = CS.Minecraft.ModificationSource.InternalOrSystem
local quaternionIdentity = CS.UnityEngine.Quaternion.identity
local ignoreExplosionsFlag = CS.Minecraft.Configurations.BlockFlags.IgnoreExplosions
local assetManager = CS.Minecraft.Assets.AssetManager.Instance
local gameModeContext = CS.Minecraft.Multiplayer.GameModeContext
local networkManager = CS.Mirror.NetworkManager
local explosionEffectAssetName = "Assets/Minecraft Default PBR Resources/Effects/Explosion Effect.prefab"
local waitTime = 3
local explodeRadius = 5

local function to_key(x, y, z)
    return string.format("%d:%d:%d", x, y, z)
end

function tnt:init(world, block)
    tnt.base.init(self, world, block)

    self.water_name = "water"
    self.lava_name = "lava"
    self.mass = 0
    self.gravity_multiplier = 0
    self.air_block_data = world.BlockDataTable:GetBlock("air")
    self.pending_explosions = {}
end

function tnt:is_lava(x, y, z, accessor)
    local block = accessor:GetBlock(x, y, z)
    return block and block.InternalName == self.lava_name
end

function tnt:place(x, y, z)
    -- 옆에 용암이 있으면 즉시 폭발
    local accessor = self.world.RWAccessor
    local flag = self:is_lava(x - 1, y, z, accessor)
        or self:is_lava(x + 1, y, z, accessor)
        or self:is_lava(x, y - 1, z, accessor)
        or self:is_lava(x, y + 1, z, accessor)
        or self:is_lava(x, y, z - 1, accessor)
        or self:is_lava(x, y, z + 1, accessor)

    if flag then
        self:click(x, y, z)
    end
end

function tnt:click(x, y, z)
    local key = to_key(x, y, z)
    if self.pending_explosions[key] then
        print(string.format("[TNT TRACE] click ignored (already pending) key=%s server=%s", key, tostring(gameModeContext.IsServer)))
        return
    end

    self.pending_explosions[key] = true
    if gameModeContext.IsServer then
        print(string.format("[TNT TRACE] server click start key=%s pos=(%d,%d,%d)", key, x, y, z))
        if gameModeContext.IsMultiplayer and networkManager.singleton then
            print(string.format("[TNT TRACE] server notify fuse started key=%s", key))
            networkManager.singleton:NotifyTntFuseStarted(x, y, z)
        end

        self.world:StartCoroutine(util.cs_generator(function()
            local elapsed = 0
            local effectAsset = assetManager:LoadAsset(explosionEffectAssetName, typeof(CS.UnityEngine.GameObject))
            local accessor = self.world.RWAccessor

            while elapsed < waitTime do
                elapsed = elapsed + unityTime.deltaTime
                coroutine.yield(nil)
            end

            local block = accessor:GetBlock(x, y, z)
            if block and block.InternalName == self.InternalName then
                print(string.format("[TNT TRACE] server fuse done explode key=%s", key))
                self:explode(x, y, z, explodeRadius, accessor)
                print(string.format("[TNT TRACE] server set air after explode key=%s", key))
                accessor:SetBlock(x, y, z, self.air_block_data, quaternionIdentity, playerModification)
            else
                print(string.format("[TNT TRACE] server fuse done but block changed key=%s block=%s", key, block and block.InternalName or "nil"))
            end

            if effectAsset.IsDone and effectAsset.Asset then
                local effect = CS.UnityEngine.Object.Instantiate(effectAsset.Asset)
                effect.transform.position = CS.UnityEngine.Vector3(x, y, z)
                local particle = CS.Minecraft.Lua.LuaUtility.GetParticleSystem(effect)
                particle:Play()
            end

            self.pending_explosions[key] = nil
            print(string.format("[TNT TRACE] server pending cleared key=%s", key))
        end))
        return
    end

    -- 클라이언트는 시각 효과만 재생하고 월드 블록은 서버 동기화에 맡김
    print(string.format("[TNT TRACE] client click visual start key=%s pos=(%d,%d,%d)", key, x, y, z))
    -- 서버 블록은 유지하되, 로컬에서는 중복 렌더링(위에 하나 더 생기는 것처럼 보이는 현상)을 방지하기 위해
    -- 비주얼 엔티티 재생 중 임시로 air 처리한다. (네트워크 전파되지 않음)
    self.world.RWAccessor:SetBlock(x, y, z, self.air_block_data, quaternionIdentity, systemModification)
    self.world.EntityManager:CreateBlockEntityAt(x, y, z, self:get_block_data())
end

function tnt:explode(center_x, center_y, center_z, radius, accessor)
    local sqrRadius = radius * radius

    for x = -radius, radius do for z = -radius, radius do for y = -radius, radius do
        if x * x + z * z + y * y <= sqrRadius then
            local world_x = center_x + x
            local world_y = center_y + y
            local world_z = center_z + z
            local block = accessor:GetBlock(world_x, world_y, world_z)

            if block then
                if block.InternalName == self.InternalName then
                    self:click(world_x, world_y, world_z) -- TNT를 폭발에 휘말리면 해당 TNT도 폭발
                elseif not block:HasFlag(ignoreExplosionsFlag) then
                    accessor:SetBlock(world_x, world_y, world_z, self.air_block_data, quaternionIdentity, playerModification)
                end
            end
        end
    end end end
end

function tnt:entity_init(entity, context)
    entity.Mass = self.mass
    entity.GravityMultiplier = self.gravity_multiplier

    local pos = entity.Position
    context.key = to_key(pos.x, pos.y, pos.z)

    entity:StartCoroutine(util.cs_generator(function()
        local time = 0
        print(string.format("[TNT TRACE] entity fuse visual start key=%s server=%s", context.key, tostring(gameModeContext.IsServer)))

        while time < waitTime do
            time = time + unityTime.deltaTime
            local t = (math.cos(time * math.pi * 1.5) + 1) * 0.5
            local color = unityColor.Lerp(unityColor.grey, unityColor.white, t)
            entity.MaterialProperty:SetColor("_MainColor", color)
            coroutine.yield(nil)
        end

        self.pending_explosions[context.key] = nil
        print(string.format("[TNT TRACE] entity fuse visual end key=%s", context.key))
        entity.EnableRendering = false
        coroutine.yield(nil)
        self.world.EntityManager:DestroyEntity(entity)
    end))
end

function tnt:entity_on_collisions(entity, flags, context)
    -- no-op
end
