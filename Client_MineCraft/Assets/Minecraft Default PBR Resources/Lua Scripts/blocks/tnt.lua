require "block"
local util = require "xlua.util"

tnt = create_block_behaviour()
local unityTime = CS.UnityEngine.Time
local playerModification = CS.Minecraft.ModificationSource.PlayerAction
local quaternionIdentity = CS.UnityEngine.Quaternion.identity
local ignoreExplosionsFlag = CS.Minecraft.Configurations.BlockFlags.IgnoreExplosions
local assetManager = CS.Minecraft.Assets.AssetManager.Instance
local explosionEffectAssetName = "Assets/Minecraft Default PBR Resources/Effects/Explosion Effect.prefab"
local waitTime = 3
local explodeRadius = 5

function tnt:init(world, block)
    tnt.base.init(self, world, block)

    self.water_name = "water"
    self.lava_name = "lava"
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
    local key = string.format("%d:%d:%d", x, y, z)

    if self.pending_explosions[key] then
        return
    end

    self.pending_explosions[key] = true
    self.world:StartCoroutine(util.cs_generator(function()
        local time = 0
        local effectAsset = assetManager:LoadAsset(explosionEffectAssetName, typeof(CS.UnityEngine.GameObject))
        local accessor = self.world.RWAccessor

        while time < waitTime or not effectAsset.IsDone do
            time = time + unityTime.deltaTime
            coroutine.yield(nil)
        end

        local block = accessor:GetBlock(x, y, z)

        -- 이미 다른 폭발로 제거된 경우 중복 처리하지 않음
        if block and block.InternalName == self.InternalName then
            -- 물속 폭발은 블록을 파괴하지 않음
            if block.InternalName ~= self.water_name then
                self:explode(x, y, z, explodeRadius, accessor)
            end

            accessor:SetBlock(x, y, z, self.air_block_data, quaternionIdentity, playerModification)
        end

        local effect = CS.UnityEngine.Object.Instantiate(effectAsset.Asset)
        effect.transform.position = CS.UnityEngine.Vector3(x, y, z)

        local particle = CS.Minecraft.Lua.LuaUtility.GetParticleSystem(effect)
        particle:Play()

        self.pending_explosions[key] = nil
    end))
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
    -- TNT는 블록 기반 타이머로 동작하고 블록 엔티티는 사용하지 않음
    self.world.EntityManager:DestroyEntity(entity)
end

function tnt:entity_on_collisions(entity, flags, context)
    -- no-op
end
