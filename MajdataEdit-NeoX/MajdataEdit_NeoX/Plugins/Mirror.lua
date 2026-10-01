-- Mirror / Rotate: Simai 谱面文本的镜像 + 45° 旋转变换。
-- 跟原 MirrorPlugin.cs 行为完全对齐：按 ','/'{'/'}'/'('/')'/'/'/'\`'/HS 结构
-- 切段，段内按映射表字符级替换。

-- 左右镜像
local MIRROR_LR_MAP = {
    ['1']='8', ['2']='7', ['3']='6', ['4']='5',
    ['5']='4', ['6']='3', ['7']='2', ['8']='1',
    ['q']='p', ['p']='q',
    ['<']='>', ['>']='<',
    ['z']='s', ['s']='z',
}
local MIRROR_LR_SPECIAL_PREFIX = { D=true, E=true }
local MIRROR_LR_SPECIAL_MAP = {
    ['8']='2', ['2']='8',
    ['3']='7', ['7']='3',
    ['4']='6', ['6']='4',
    ['1']='1', ['5']='5',
}

-- 上下镜像
local MIRROR_UD_MAP = {
    ['4']='1', ['5']='8', ['6']='7', ['3']='2',
    ['7']='6', ['2']='3', ['8']='5', ['1']='4',
    ['q']='p', ['p']='q',
    ['z']='s', ['s']='z',
}
local MIRROR_UD_SPECIAL_PREFIX = { D=true, E=true }
local MIRROR_UD_SPECIAL_MAP = {
    ['4']='2', ['2']='4',
    ['1']='5', ['5']='1',
    ['8']='6', ['6']='8',
    ['3']='3', ['7']='7',
}

-- 顺时针 45° 旋转
local ROTATE_CW_45_MAP = {
    ['8']='1', ['7']='8', ['6']='7', ['5']='6',
    ['4']='5', ['3']='4', ['2']='3', ['1']='2',
}
local ROTATE_CW_45_SPECIAL_PREFIX = { ['2']=true, ['6']=true }

-- 逆时针 45° 旋转
local ROTATE_CCW_45_MAP = {
    ['1']='8', ['2']='1', ['3']='2', ['4']='3',
    ['5']='4', ['6']='5', ['7']='6', ['8']='7',
}
local ROTATE_CCW_45_SPECIAL_PREFIX = { ['3']=true, ['7']=true }

local ROTATE_45_SPECIAL_MAP = {
    ['<']='>', ['>']='<',
}

local HS_SEQUENCE = '<HS*'

local function map_lookup(map, c)
    return map[c]
end

local function is_white(c)
    return c == ' ' or c == '\t' or c == '\n' or c == '\r'
end

-- 对单个 part（已经按分隔符切好）按映射表变换
local function normal_mirror_part(str, normal_map, special_map, special_prefix)
    local result, is_special_prefix, is_in_bracket = '', false, false
    for i = 1, #str do
        local c = string.sub(str, i, i)
        if is_white(c) then
            result = result .. c
        elseif is_in_bracket or c == '[' then
            result = result .. c
        elseif is_special_prefix then
            is_special_prefix = false
            local v = map_lookup(special_map, c)
            if v ~= nil then
                result = result .. v
            elseif tonumber(c) ~= nil then
                local v2 = map_lookup(normal_map, c)
                if v2 ~= nil then result = result .. v2 else result = result .. c end
            else
                result = result .. c
            end
        else
            local v = map_lookup(normal_map, c)
            if v ~= nil then result = result .. v else result = result .. c end
        end

        if c == '[' then
            is_in_bracket = true
        elseif c == ']' then
            is_in_bracket = false
        end
        if special_prefix[c] then is_special_prefix = true end
    end
    return result
end

local function note_mirror_part(str, handle_type)
    if handle_type == 'LR' then
        return normal_mirror_part(str, MIRROR_LR_MAP, MIRROR_LR_SPECIAL_MAP, MIRROR_LR_SPECIAL_PREFIX)
    elseif handle_type == 'UD' then
        return normal_mirror_part(str, MIRROR_UD_MAP, MIRROR_UD_SPECIAL_MAP, MIRROR_UD_SPECIAL_PREFIX)
    elseif handle_type == '180' then
        str = normal_mirror_part(str, MIRROR_LR_MAP, MIRROR_LR_SPECIAL_MAP, MIRROR_LR_SPECIAL_PREFIX)
        str = normal_mirror_part(str, MIRROR_UD_MAP, MIRROR_UD_SPECIAL_MAP, MIRROR_UD_SPECIAL_PREFIX)
        return str
    elseif handle_type == 'CW45' then
        return normal_mirror_part(str, ROTATE_CW_45_MAP, ROTATE_45_SPECIAL_MAP, ROTATE_CW_45_SPECIAL_PREFIX)
    elseif handle_type == 'CCW45' then
        return normal_mirror_part(str, ROTATE_CCW_45_MAP, ROTATE_45_SPECIAL_MAP, ROTATE_CCW_45_SPECIAL_PREFIX)
    end
    return str
end

-- 主调度：把 str 按 '{' '}' '(' ')' ',' '/' '\`'/HS 结构切段，段间原样，段内镜像
local function handle_mirror(str, handle_type)
    local result, cur, is_part_ignored, hs_status = '', '', false, 0
    local hs_len = #HS_SEQUENCE
    for i = 1, #str do
        local c = string.sub(str, i, i)
        cur = cur .. c

        if not is_part_ignored and (c == '{' or c == '}' or c == '(' or c == ')') then
            is_part_ignored = true
        end

        if hs_status == 0 then
            if string.sub(HS_SEQUENCE, 1, 1) == c then hs_status = 1 end
        elseif hs_status ~= hs_len then
            if not is_white(c) then
                if string.sub(HS_SEQUENCE, hs_status + 1, hs_status + 1) == c then
                    hs_status = hs_status + 1
                    if hs_status == hs_len then is_part_ignored = true end
                else
                    hs_status = 0
                end
            end
        end

        local is_end =
            c == '}' or c == ')' or c == ',' or c == '/' or c == '`' or
            (hs_status == 4 and c == '>')
        if is_end then
            if is_part_ignored then
                result = result .. cur
            else
                result = result .. note_mirror_part(cur, handle_type)
            end
            is_part_ignored = false
            hs_status = 0
            cur = ''
        end
    end

    if #cur > 0 then
        if is_part_ignored then
            result = result .. cur
        else
            result = result .. note_mirror_part(cur, handle_type)
        end
    end

    return result
end

function mirror_h  (s) return handle_mirror(s, 'LR')   end
function mirror_v  (s) return handle_mirror(s, 'UD')   end
function mirror_180(s) return handle_mirror(s, '180')  end
function rotate_r  (s) return handle_mirror(s, 'CW45') end
function rotate_l  (s) return handle_mirror(s, 'CCW45') end

-- 元数据用数组（连续 1..N 整数键）返回，Lua 的 array part 严格按 1..N 顺序，
-- 菜单项顺序 = Lua 文件里写的顺序。C# 端通过整数索引遍历。
-- 每个 entry: Process = 处理函数 / NameKey = Langs 表 key / Icon = 图标 key / Name = fallback 显示名
return {
    { Process = mirror_h,   NameKey = 'Gui_MirrorHorizontally', Icon = 'mirror_h'   },
    { Process = mirror_v,   NameKey = 'Gui_MirrorVertically',   Icon = 'mirror_v'   },
    { Process = mirror_180, NameKey = 'Gui_Mirror180',          Icon = 'mirror_180' },
    { Process = rotate_r,   NameKey = 'Gui_Rotate45',           Icon = 'rotate_r'   },
    { Process = rotate_l,   NameKey = 'Gui_RotateNeg45',        Icon = 'rotate_l'   },
}