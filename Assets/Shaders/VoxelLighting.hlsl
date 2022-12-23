// based on https://nedmakesgames.medium.com/creating-custom-lighting-in-unitys-shader-graph-with-universal-render-pipeline-5ad442c27276

#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

struct CustomLightingData
{
    float3 normalWS;
    float4 albedo;
    float3 positionWS;
    float4 vertexColor;
    float4 shadowCoord;
};

extern float3 _SunlightColor;

#ifndef SHADERGRAPH_PREVIEW
    float4 CustomLightHandling(CustomLightingData d, Light light)
    {
        float3 radiance = light.color * sqrt(light.distanceAttenuation) * light.shadowAttenuation;
        float diffuse = saturate(dot(d.normalWS, light.direction));

        float3 colorRGB = radiance * diffuse * d.albedo.xyz;

        float4 color;
        color.xyz = colorRGB.xyz;
        color.w = d.albedo.w;

        return color;     
    }
    float4 GetLightMappingColor(CustomLightingData d)
    {
        float3 blockLightColor = d.vertexColor.xyz;
        float3 sunLightColor = d.vertexColor.w * _SunlightColor;

        float3 maxLightColor = max(blockLightColor, sunLightColor);

        float3 colorRGB = d.albedo.xyz * maxLightColor;

        float4 color;
        color.xyz = colorRGB.xyz;
        color.w = 1.0;

        return color;     
    }
#endif

float4 CalculateCustomLighting(CustomLightingData d)
{
    float4 color = 0;

    #ifndef SHADERGRAPH_PREVIEW
        //Light mainLight = GetMainLight(d.shadowCoord, d.positionWS, 1);
        //color += CustomLightHandling(d, mainLight);

        color += GetLightMappingColor(d) * 3;

        #ifdef _ADDITIONAL_LIGHTS
            uint numAdditionalLights = GetAdditionalLightsCount();
            for (uint lightIdx = 0; lightIdx < numAdditionalLights; lightIdx++)
            {
                Light light = GetAdditionalLight(lightIdx, d.positionWS, 1);
                color += CustomLightHandling(d, light) * 2;
            }
        #endif

        color = clamp(color, float4(0, 0, 0, 0), float4(d.albedo.xyz * 1.5, 1));
        //color = clamp(color, float4(0, 0, 0, 0), float4(1, 1, 1, 1));

    #else
        color = d.albedo;
    #endif

    return color;
}

#ifdef SHADERGRAPH_PREVIEW
    void CalculateCustomLighting_float(float3 Position, float3 Normal, float4 Albedo, float4 VertexColor, out float4 Color)
    {
        CustomLightingData d;
        d.normalWS = Normal;
        d.albedo = Albedo;
        d.positionWS = Position;
        d.vertexColor = VertexColor;
        d.shadowCoord = 0;

        Color = CalculateCustomLighting(d);
    }

#else
    // Using float4 for colors in case we need to add support for transparency later
    void CalculateCustomLighting_float(float3 Position, float3 Normal, float4 Albedo, float4 VertexColor, out float4 Color)
    {
        CustomLightingData d;
        d.normalWS = Normal;
        d.albedo = Albedo;
        d.positionWS = Position;
        d.vertexColor = VertexColor;

        #ifdef SHADERGRAPH_PREVIEW
            d.shadowCoord = 0;
        #else
            float4 positionCS = TransformWorldToHClip(Position);
            #if SHADOWS_SCREEN
                d.shadowCoord = ComputeScreenPos(positionCS);
            #else
                d.shadowCoord = TransformWorldToShadowCoord(Position);
            #endif
        #endif

        Color = CalculateCustomLighting(d);
    }
#endif


#endif