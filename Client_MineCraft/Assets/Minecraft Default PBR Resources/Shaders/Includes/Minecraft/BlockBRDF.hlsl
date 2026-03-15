#ifndef MINECRAFT_BLOCK_BRDF_INCLUDED
#define MINECRAFT_BLOCK_BRDF_INCLUDED

#include "Includes/Minecraft/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct BlockBRDFData
{
    half3 albedo;
    half metallic;
    half emission;
    half roughness;
    half skyLight;
    half blockLight;
    float3 positionWS;
    float3 normalWS;
    float3 viewDirWS;
    float4 shadowCoord;
};

inline half3 FresnelTerm(half3 c, half cosA)
{
    half t = pow(1 - cosA, 5);
    return c + (1 - c) * t;
}

inline half3 FresnelLerp(half3 c0, half3 c1, half cosA)
{
    half t = pow(1 - cosA, 5);
    return lerp(c0, c1, t);
}

inline void InitializeBlockBRDFData(half4 albedo, half4 mer, float3 positionWS, float3 normalWS, float3 lights, float3 viewDirWS, float4 shadowCoord, out BlockBRDFData data)
{
    data = (BlockBRDFData)0;
    data.albedo = albedo.rgb;
    data.metallic = mer.r;
    data.emission = mer.g * lights.x;
    data.roughness = mer.b;
    data.skyLight = lights.y;
    data.blockLight = lights.z;
    data.positionWS = positionWS;
    data.normalWS = normalWS;
    data.viewDirWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    data.shadowCoord = shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    data.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    data.shadowCoord = float4(0, 0, 0, 0);
#endif
}

inline half4 BlockFragmentPBR(BlockBRDFData input, half alpha)
{
    half oneMinusReflectivity = OneMinusReflectivityMetallic(input.metallic);

    half3 diffColor = input.albedo * oneMinusReflectivity;
    half3 specColor = lerp(kDieletricSpec.rgb, input.albedo, input.metallic);

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    half3 viewDirWS = SafeNormalize(input.viewDirWS);
    half3 reflectVector = reflect(-viewDirWS, normalWS);

    Light mainLight = GetMainLight(input.shadowCoord);
    half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
    half nv = saturate(dot(normalWS, viewDirWS));
    half nl = saturate(dot(normalWS, mainLight.direction));
    half nh = saturate(dot(normalWS, halfDir));
    half lv = saturate(dot(mainLight.direction, viewDirWS));
    half lh = saturate(dot(mainLight.direction, halfDir));

    half isDay = saturate(mainLight.direction.y); // 낮은 1, 밤은 0, 전환 시 (0,1)
    half receiveSkyLight = pow(input.skyLight, 2); // 직사광이면 1, 아니면 [0,1)의 작은 값
    half skyLightLevel = lerp(_LightLimits.x, _LightLimits.y, input.skyLight * nl) * isDay; // 낮에만 태양광 적용
    half perceptualRoughness = RoughnessToPerceptualRoughness(input.roughness);

    half3 dayDiffuse = diffColor * DisneyDiffuse(nv, nl, lv, perceptualRoughness) * skyLightLevel;
    half3 nightColor = (1 - isDay) * 0.02 * diffColor; // 밤이 완전히 검지 않도록 약간 밝게
    half3 diffuseTerm = (dayDiffuse + nightColor) * receiveSkyLight; // 태양광이 없는 곳은 최대한 어둡게

    half DV = DV_SmithJointGGX(nh, nl, nv, input.roughness);
    half3 F = FresnelTerm(specColor, lh);
    half3 specularTerm = DV * F * nl * isDay * receiveSkyLight; // 낮이며 직사광일 때만 존재

    half shadowAttenuation = lerp(1, mainLight.shadowAttenuation, isDay); // 밤에는 그림자 없음

    half3 skyLightTerm = PI * (diffuseTerm + specularTerm) * mainLight.color * mainLight.distanceAttenuation * shadowAttenuation;
    half3 blockLightTerm = (diffColor + specColor) * input.blockLight; // 블록 광원은 그림자 영향 없음
    half3 emissionTerm = input.emission * input.albedo; // 자체 발광

    half4 color = half4(emissionTerm + max(skyLightTerm, blockLightTerm), alpha);

    // fade
    float dis = distance(input.positionWS.xz, GetCameraPositionWS().xz);
    half4 worldAmbientColor = lerp(_WorldAmbientColorNight, _WorldAmbientColorDay, isDay);
    return EaseIn(color, worldAmbientColor, saturate(dis / max(min(_RenderDistance, _ViewDistance), 0.01)));
}

#endif // MINECRAFT_BLOCK_BRDF_INCLUDED