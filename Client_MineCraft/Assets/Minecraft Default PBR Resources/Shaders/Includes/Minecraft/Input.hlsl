#ifndef MINECRAFT_INPUT_INCLUDED
#define MINECRAFT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 모든 블록 텍스처(노멀 맵, PBR 맵 포함)
TEXTURE2D_ARRAY(_BlockTextures); SAMPLER(sampler_BlockTextures);

// 블록 채굴 진행도 텍스처
TEXTURE2D_ARRAY(_DigProgressTextures); SAMPLER(sampler_DigProgressTextures);

// 블록 채굴 진행도(텍스처 인덱스)
int _DigProgress;

// 현재 플레이어 조준점이 가리키는 블록의 월드 좌표
float3 _TargetBlockPosition;

// 렌더 거리(블록 단위)
int _RenderDistance;

// 시야 거리(블록 단위)
int _ViewDistance;

// 광원 제한
// x - 최소 광원 레벨 [0, 0.5]
// y - 최대 광원 레벨 [0.5, 1]
half2 _LightLimits;

// 낮 환경색: XOZ 평면에서 카메라와 멀수록 이 색에 가까워짐
half4 _WorldAmbientColorDay;

// 밤 환경색: XOZ 평면에서 카메라와 멀수록 이 색에 가까워짐
half4 _WorldAmbientColorNight;

#endif // MINECRAFT_INPUT_INCLUDED