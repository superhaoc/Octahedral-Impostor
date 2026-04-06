Shader "Impostors/OctahedralImpostor_AlbedoOnly"
{
    Properties
    {
        _MainTex ("Albedo Atlas", 2D) = "white" {}
        _TilesPerSide ("Tiles Per Side", Float) = 32
        _ImpostorSize ("Impostor Size", Float) = 1.0
    }
    SubShader
    {
        Cull Back

        Blend One OneMinusSrcAlpha

        Tags {"QUEUE" = "Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD1;
                float4 frame0UV : TEXCOORD2;
                float4 frame1UV : TEXCOORD3;
                float3 newLocalPos : TEXCOORD4;
            };


            struct Ray
            {
                half3 Origin;
                half3 Direction;
            };


            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _TilesPerSide;
            float _ImpostorSize;

            float2 DirToOctahedronUV(float3 vec)
            {
                // float3 p = dir / (abs(dir.x) + abs(dir.y) + abs(dir.z));
                // float2 uv = p.xy;
                // if (p.z < 0.0)
                // {
                //     uv = (1.0 - abs(uv.yx)) * (uv.xy >= 0.0 ? 1.0 : -1.0);
                // }
                // return uv * 0.5 + 0.5;

                 vec.xz /= dot( 1,  abs(vec) );
                if ( vec.y <= 0 )
                {
                    half2 flip = vec.xz >= 0 ? half2(1,1) : half2(-1,-1);
                    vec.xz = (1-abs(vec.zx)) * flip;
                }
                return vec.xz * 0.5 + 0.5;
            }

            //for sphere
            half3 OctaSphereEnc( half2 coord )
            {
                half3 vec = half3( coord.x, 1-dot(1,abs(coord)), coord.y );
                if ( vec.y < 0 )
                {
                    half2 flip = vec.xz >= 0 ? half2(1,1) : half2(-1,-1);
                    vec.xz = (1-abs(vec.zx)) * flip;
                }
                return vec;
            }

            
            half4 TriangleInterpolate( half2 uv )
            {
                uv = frac(uv);

                half2 omuv = half2(1.0,1.0) - uv.xy;
                
                half4 res = half4(0,0,0,0);
                //frame 0
                res.x = min(omuv.x,omuv.y); 
                //frame 1
                res.y = abs( dot( uv, half2(1.0,-1.0) ) );
                //frame 2
                res.z = min(uv.x,uv.y); 
                //mask
                res.w = saturate(ceil(uv.x-uv.y));
                
                return res;
            }

            half3 FrameXYToRay( half2 frame, half2 frameCountMinusOne )
            {
                //divide frame x y by framecount minus one to get 0-1
                half2 f = frame.xy / frameCountMinusOne;
                //bias and scale to -1 to 1
                f = (f-0.5)*2.0; 
                //convert to vector, either full sphere or hemi sphere
                half3 vec = OctaSphereEnc( f );
                vec = normalize(vec);
                return vec;
            }


            half2 VirtualPlaneUV( half3 planeNormal, half3 planeX, half3 planeZ, half3 center, half2 uvScale, Ray rayLocal )
            {
                half normalDotOrigin = dot(planeNormal,rayLocal.Origin);
                half normalDotCenter = dot(planeNormal,center);
                half normalDotRay = dot(planeNormal,rayLocal.Direction);
                
                half planeDistance = normalDotOrigin-normalDotCenter;
                planeDistance *= -1.0;
                
                half intersect = planeDistance / normalDotRay;
                
                half3 intersection = ((rayLocal.Direction * intersect) + rayLocal.Origin) - center;
                
                half dx = dot(planeX,intersection);
                half dz = dot(planeZ,intersection);
                
                half2 uv = half2(0,0);
                
                if ( intersect > 0 )
                {
                    uv = half2(dx,dz);
                }
                else
                {
                    uv = half2(0,0);
                }
                
                uv /= uvScale;
                uv += half2(0.5,0.5);
                return uv;
            }

            half3 SpriteProjection( half3 pivotToCameraRayLocal, half frames, half2 size, half2 coord )
            {
                half3 gridVec = pivotToCameraRayLocal;
                
                //octahedron vector, pivot to camera
                half3 y = normalize(gridVec);
                
                half3 x = normalize( cross( y, half3(0.0, 1.0, 0.0) ) );
                half3 z = normalize( cross( x, y ) );

                half2 uv = ((coord*frames)-0.5) * 2.0; //-1 to 1 

                half3 newX = x * uv.x;
                half3 newZ = z * uv.y;
                
                half2 halfSize = size*0.5;
                
                newX *= halfSize.x;
                newZ *= halfSize.y;
                
                half3 res = newX + newZ;  
                
                return res;
            }



            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldOrigin = float3(UNITY_MATRIX_M[0].w,UNITY_MATRIX_M[1].w,UNITY_MATRIX_M[2].w);
                float3 worldCameraPos = _WorldSpaceCameraPos;
                float3 objectViewDir = TransformWorldToObject(worldCameraPos);
                objectViewDir = normalize(objectViewDir);
                float3 upVector = float3(0, 1, 0);
                float3 rightVector = normalize(cross(upVector,objectViewDir));
                float3 upVectorCorrect = cross(rightVector,objectViewDir);
                float3 forwardVector = cross(rightVector,upVectorCorrect);
                float2 offset = (IN.uv - 0.5) * _ImpostorSize;
                float3 newLocalPos = rightVector * offset.x + upVectorCorrect * offset.y;

                OUT.positionCS = TransformObjectToHClip(newLocalPos);
                OUT.uv = IN.uv;
                OUT.newLocalPos = newLocalPos;
                return OUT;
            }

            //为了计算准确性为在vertex 计算三点uv并插值, 取而代之是在fragment 里实时计算uv，performance heavy!!
            half4 frag(Varyings IN) : SV_Target
            {
             
                float3 worldCameraPos = _WorldSpaceCameraPos;
                float3 objectCameraPos = TransformWorldToObject(worldCameraPos);
                float3 objectViewDir = objectCameraPos;//TransformWorldToObjectDir(worldCameraPos - worldOrigin);
                objectViewDir = normalize(objectViewDir);


                Ray rayLocal;
                rayLocal.Origin = objectCameraPos; 
                rayLocal.Direction = normalize(IN.newLocalPos - objectCameraPos); 

                float2 octUV = DirToOctahedronUV(objectViewDir);

                // 计算 tile 索引
                float2 scaled = octUV * _TilesPerSide;
                float2 tileIndex = floor(scaled);
                float2 tileUV = frac(scaled); //有问题
               // tileIndex = clamp(tileIndex, 0, _TilesPerSide - 1);

             

                float4 weights = TriangleInterpolate(tileUV);
                half2 frame0 = tileIndex;
                half2 frame1 = tileIndex + lerp(half2(0,1),half2(1,0),weights.w);
                half2 frame2 = tileIndex + half2(1,1);

                 //convert frame coordinate to octahedron direction
                half3 frame0ray = FrameXYToRay(frame0, _TilesPerSide - 1);
                half3 frame1ray = FrameXYToRay(frame1, _TilesPerSide - 1);
                half3 frame2ray = FrameXYToRay(frame2, _TilesPerSide - 1);


                half3 planeCenter = half3(0,0,0);

                half2 size = 1.0;

                half3 plane0normal = frame0ray;
                half3 plane0x = normalize( half3(-frame0ray.z, 0, frame0ray.x) );
                half3 plane0z = normalize( cross(plane0x, frame0ray ) ); 

                half2 vUv0 = VirtualPlaneUV( plane0normal, plane0x, plane0z, planeCenter, _ImpostorSize, rayLocal );
                vUv0 /= _TilesPerSide.xx; 


                //-----------------------------------------------------------
               
                half3 plane1normal = frame1ray;
                half3 plane1x = normalize( half3(-frame1ray.z, 0, frame1ray.x) );
                half3 plane1z = normalize( cross(plane1x, frame1ray ) );;
 
                
                //virtual plane UV coordinates
                half2 vUv1 = VirtualPlaneUV( plane1normal, plane1x, plane1z, planeCenter, _ImpostorSize, rayLocal );
                vUv1 /= _TilesPerSide.xx;


                
                half3 plane2normal = frame2ray;
                half3 plane2x = normalize( half3(-frame2ray.z, 0, frame2ray.x) );;
                half3 plane2z = normalize( cross(plane2x, frame2ray ) );;
               
                
                //virtual plane UV coordinates
                half2 vUv2 = VirtualPlaneUV( plane2normal, plane2x, plane2z, planeCenter, _ImpostorSize, rayLocal );
                vUv2 /= _TilesPerSide.xx;



                half2 frame0_ = tileIndex / _TilesPerSide;
                half2 frame1_ = (tileIndex + lerp(half2(0,1),half2(1,0),weights.w))/_TilesPerSide;
                half2 frame2_ = (tileIndex + half2(1,1)) / _TilesPerSide;


                // float2 atlasUV0 = (frame0 + IN.uv) / _TilesPerSide;
                // float2 atlasUV1 = (frame1 + IN.uv) / _TilesPerSide;
                // float2 atlasUV2 = (frame2 + IN.uv) / _TilesPerSide;
                
                half2 gridSize = 1.0/_TilesPerSide.xx;
                float2 vp0uv = frame0_ + vUv0;
                float2 vp1uv = frame1_ + vUv1;
                float2 vp2uv = frame2_ + vUv2;


                vp0uv = clamp(vp0uv,frame0_,frame0_+gridSize);
                vp1uv = clamp(vp1uv,frame1_,frame1_+gridSize);
                vp2uv = clamp(vp2uv,frame2_,frame2_+gridSize);


                half4 color0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, vp0uv);
                half4 color1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, vp1uv);
                half4 color2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, vp2uv);


                half4 result = color0*weights.x + color1*weights.y + color2*weights.z;
                return result;
  
            }
            ENDHLSL
        }
    }
}