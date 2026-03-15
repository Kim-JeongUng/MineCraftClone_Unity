local behaviour = {} -- 기본 동작

function behaviour:init(world, block)
    self.world = world
    self.__block = block
    print("init block behaviour: " .. block.InternalName)
end

function behaviour:tick(x, y, z)
    -- default implement
end

function behaviour:place(x, y, z)
    -- default implement
end

function behaviour:destroy(x, y, z)
    -- default implement
end

function behaviour:click(x, y, z)
    -- default implement
end

function behaviour:entity_init(entity, context)
    -- default implement
end

function behaviour:entity_destroy(entity, context)
    -- default implement
end

function behaviour:entity_update(entity, context)
    -- default implement
end

function behaviour:entity_fixed_update(entity, context)
    -- default implement
end

function behaviour:entity_on_collisions(entity, flags, context)
    -- default implement
end

function behaviour:get_block_data()
    return self.__block
end

--- 블록 동작 객체를 생성합니다.
---- `base` 인자를 전달하지 않으면 기본 동작 객체를 반환합니다. 
---- `base` 인자를 전달하면 `base`의 모든 동작을 상속합니다.
---- @param base? table
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
