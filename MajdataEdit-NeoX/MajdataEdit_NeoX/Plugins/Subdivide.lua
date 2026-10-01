-- Subdivide: 把 {N}... 区块里的逗号数 × multiplier，得到新的细分。
-- 跟原 SubdividePlugin.cs 行为完全对齐；非整数细分直接原样返回。

local function subdivide_internal(phrase, multiplier)
    if multiplier <= 1 then return phrase end

    local s = string.find(phrase, '{', 1, true)
    if s == nil then return phrase end
    s = s + 1
    local e = string.find(phrase, '}', s, true)
    if e == nil or e <= s then return phrase end

    local original_sub = tonumber(string.sub(phrase, s, e - 1))
    if original_sub == nil then return phrase end
    local content = string.sub(phrase, e + 1)

    local new_sub = original_sub * multiplier
    local diff = math.abs(math.floor(new_sub) - new_sub)
    if not (diff < 1e-7 or diff > 1 - 1e-7) then return phrase end

    local comma_count = 0
    for _ in string.gmatch(content, ',') do comma_count = comma_count + 1 end
    local new_comma = comma_count * multiplier
    local diff1 = math.abs(math.floor(new_comma) - new_comma)
    if not (diff1 < 1e-7 or diff1 > 1 - 1e-7) then return phrase end

    local out, fractional = {}, 0.0
    for c in string.gmatch(content, '.') do
        if c == ',' then
            fractional = fractional + multiplier
            local repeat_count = math.floor(fractional)
            fractional = fractional - repeat_count
            out[#out + 1] = string.rep(',', repeat_count)
        else
            out[#out + 1] = c
        end
    end

    return '{' .. tostring(new_sub) .. '}' .. table.concat(out)
end

-- 按 '{...}' 区块逐个处理（保留分隔；空段丢弃，对应 C# Split + RemoveEmptyEntries）
local function subdivide(phrase, multiplier)
    local result = ''
    for piece in string.gmatch(phrase, '({[^{]-})') do
        result = result .. subdivide_internal(piece, multiplier)
    end
    return result
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