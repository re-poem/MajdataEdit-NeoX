/*
    miniaudio 的编译单元，额外启用 Vorbis (Ogg) 解码。

    上游的 miniaudio.c 只有 "#define MINIAUDIO_IMPLEMENTATION + #include miniaudio.h"，
    这样 STB_VORBIS_INCLUDE_STB_VORBIS_H 永远不被定义，miniaudio.h 里那段
    Vorbis 后端就被整体编译掉，track.ogg 解码直接返回 MA_INVALID_FILE (-10)。
    （MA_ENABLE_ONLY_SPECIFIC_BACKENDS 那组宏并不含 MA_NO_VORBIS，所以不是被显式关掉的。）

    这里按上游 tools/audioconverter/audioconverter.c 的顺序，把 extras/stb_vorbis.c
    一并编进来。ThirdParty/miniaudio 是 submodule，所以修正放在本文件而不是改它。

    include 路径由 miniaudio.targets 通过 /I 或 -I 传入 $(MiniaudioRoot)。
*/

/* stb_vorbis 用 fopen 而不是 fopen_s，MSVC 会报 C4996；/W0 已经压掉，这里再显式声明。 */
#define _CRT_SECURE_NO_WARNINGS

/*
    第一遍只取头：声明 stb_vorbis 的 API，并定义 STB_VORBIS_INCLUDE_STB_VORBIS_H，
    这正是 miniaudio.h 用来判断要不要编 Vorbis 后端的开关。
*/
#define STB_VORBIS_HEADER_ONLY
#include "extras/stb_vorbis.c"

#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio.h"

/* 第二遍取实现。 */
#undef STB_VORBIS_HEADER_ONLY
#include "extras/stb_vorbis.c"
