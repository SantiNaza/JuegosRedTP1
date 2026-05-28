Shader "Custom/DianaSenoidal"
{
    Properties
    {
        _Color1 ("Color 1", Color) = (1, 1, 1, 1)
        _Color2 ("Color 2", Color) = (1, 0, 0, 1)
        _Frequency ("Frecuencia del Seno", Float) = 20.0
        _Threshold ("Umbral (Threshold)", Range(-1.0, 1.0)) = 0.0
        _MaskRadius ("Radio de la Máscara", Float) = 0.5
    }
    SubShader
    {
        // Etiquetas necesarias para renderizar transparencia correctamente
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // Configuración de Blending para que el Alpha funcione
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            // Declaración de variables del Inspector
            float4 _Color1;
            float4 _Color2;
            float _Frequency;
            float _Threshold;
            float _MaskRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Pasamos las coordenadas de objeto (locales) al fragment shader
                o.localPos = v.vertex.xyz; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Distancia al centro en coordenadas locales (asumiendo un Quad que usa el plano XY)
                float dist = length(i.localPos.xy);

                // 2. Evaluar el seno en base a la distancia y la frecuencia
                float wave = sin(dist * _Frequency);

                // 3. Aplicar el step para el threshold
                // step(a, x) devuelve 1 si x >= a, y 0 si x < a.
                // Como sin() va de -1 a 1, el threshold está configurado en ese rango.
                float pattern = step(_Threshold, wave);

                // 4. Alternar entre los dos colores (0 = _Color1, 1 = _Color2)
                fixed4 finalColor = lerp(_Color1, _Color2, pattern);

                // 5. Crear la máscara circular limitando por el radio definido
                // Devuelve 1 cuando el radio es mayor o igual a la distancia, 0 en caso contrario.
                float mask = step(dist, _MaskRadius);

                // 6. Multiplicar el canal Alpha por la máscara para hacer transparente el exterior
                finalColor.a *= mask;

                return finalColor;
            }
            ENDCG
        }
    }
}