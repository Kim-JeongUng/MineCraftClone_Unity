require "block"
local util = require "xlua.util"

tnt = create_block_behaviour()
local unityTime = CS.UnityEngine.Time
local unityColor = CS.UnityEngine.Color
local playerModification = CS.Minecraft.ModificationSource.PlayerAction
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
    self.mass = 1
    self.gravity_multiplier = 1
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
        return
    end

    self.pending_explosions[key] = true
    if gameModeContext.IsServer then
        if gameModeContext.IsMultiplayer and networkManager.singleton then
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
                self:explode(x, y, z, explodeRadius, accessor)
                accessor:SetBlock(x, y, z, self.air_block_data, quaternionIdentity, playerModification)
            end

            if effectAsset.IsDone and effectAsset.Asset then
                local effect = CS.UnityEngine.Object.Instantiate(effectAsset.Asset)
                effect.transform.position = CS.UnityEngine.Vector3(x, y, z)
                local particle = CS.Minecraft.Lua.LuaUtility.GetParticleSystem(effect)
                particle:Play()
            end

            self.pending_explosions[key] = nil
        end))
        return
    end

    -- 클라이언트는 시각 효과만 재생하고 월드 블록은 서버 동기화에 맡김
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

        while time < waitTime do
            time = time + unityTime.deltaTime
            local t = (math.cos(time * math.pi * 1.5) + 1) * 0.5
            local color = unityColor.Lerp(unityColor.grey, unityColor.white, t)
            entity.MaterialProperty:SetColor("_MainColor", color)
            coroutine.yield(nil)
        end

        self.pending_explosions[context.key] = nil
        entity.EnableRendering = false
        coroutine.yield(nil)
        self.world.EntityManager:DestroyEntity(entity)
    end))
end

function tnt:entity_on_collisions(entity, flags, context)
    -- no-op
end
