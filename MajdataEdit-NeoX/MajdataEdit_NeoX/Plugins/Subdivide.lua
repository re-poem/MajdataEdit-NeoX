-- 分音：将 {N} 的时间格映射到更细的网格，保持已有音符的时刻不变。
-- 若细分后音符落在非整数格，保留该区块，避免悄悄改变谱面时序。
local function subdivide_internal(phrase, multiplier)
    local close = string.find(phrase, '}', 2, true)
    if not close then return phrase end

    local original_sub = tonumber(string.sub(phrase, 2, close - 1))
    if not original_sub or original_sub <= 0 or original_sub % 1 ~= 0 then return phrase end
    local new_sub = original_sub * multiplier
    if new_sub % 1 ~= 0 then return phrase end

    local content = string.sub(phrase, close + 1)
    local slots = {}
    local start = 1
    while true do
        local comma = string.find(content, ',', start, true)
        if not comma then
            slots[#slots + 1] = string.sub(content, start)
            break
        end
        slots[#slots + 1] = string.sub(content, start, comma - 1)
        start = comma + 1
    end

    local commas = #slots - 1
    local new_commas = commas * multiplier
    -- 保留完整时间格跨度；选区若在半格结束，则不改变它。
    if new_commas % 1 ~= 0 then return phrase end

    local mapped = {}
    for index, text in ipairs(slots) do
        if text ~= '' then
            local position = (index - 1) * multiplier
            if position % 1 ~= 0 then return phrase end
            mapped[position + 1] = text
        end
    end

    local out = {}
    for position = 0, new_commas do
        out[#out + 1] = mapped[position + 1] or ''
    end
    return '{' .. string.format('%d', new_sub) .. '}' .. table.concat(out, ',')
end

local function subdivide(phrase, multiplier)
    if multiplier <= 1 then return phrase end
    local out = {}
    local cursor = 1
    while true do
        local open = string.find(phrase, '{', cursor, true)
        if not open then
            out[#out + 1] = string.sub(phrase, cursor)
            break
        end
        local close = string.find(phrase, '}', open + 1, true)
        if not close then
            out[#out + 1] = string.sub(phrase, cursor)
            break
        end
        out[#out + 1] = string.sub(phrase, cursor, open - 1)
        local next_open = string.find(phrase, '{', close + 1, true)
        out[#out + 1] = subdivide_internal(string.sub(phrase, open, next_open and next_open - 1 or nil), multiplier)
        if not next_open then break end
        cursor = next_open
    end
    return table.concat(out)
end

-- process functions (顶层 key；LuaPlugin 通过 GetFunction("subdivide_1p5x") 拿到)
function subdivide_1p5x(s) return subdivide(s, 1.5) end
function subdivide_2x  (s) return subdivide(s, 2.0) end

-- 元数据用数组（连续 1..N 整数键）返回，菜单项顺序 = Lua 文件里写的顺序。
-- C# 端通过整数索引遍历；Lua 5.4 string-key iteration 顺序未定义，所以不能用 table-style。
return {
    { Process = subdivide_1p5x, NameKey = 'Gui_Subdivide1p5x', Icon = '' },
    { Process = subdivide_2x,   NameKey = 'Gui_Subdivide2x',   Icon = '' },
}