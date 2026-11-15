// Appended after each original character's palette/pattern calculation.
void ApplySuccessCostumeWear(inout SurfaceData s,inout InputData d,float3 local,float2 uv)
{
    float2 p=(local.xy+local.z*float2(.47,.83))*4.1;
    float grit=Grain(p*21), soil=Grain(p*2.7+41);
    float weave=sin(uv.x*1500)*sin(uv.y*1500);
    float mud=smoothstep(.38,.76,soil)*.34;
    half3 cloth=s.albedo*(.91+grit*.12+weave*.025);
    cloth=lerp(cloth,cloth*half3(.35,.29,.21),mud);
    float stain=smoothstep(.52,.68,Grain(p*3.1+17)+Noise(p*35)*.11)*_EndingBlood;
    // Keep dark eyes/embroidery and the selected outfit palette legible.
    stain*=smoothstep(.02,.18,dot(s.albedo,half3(.3,.59,.11)));
    s.albedo=lerp(cloth,lerp(half3(.055,.012,.008),half3(.19,.036,.021),grit),stain);
    float height=grit*.025+weave*.00025+stain*.008;
    float3 n=d.normalWS,dx=ddx(d.positionWS),dy=ddy(d.positionWS);
    float3 a=cross(dy,n),b=cross(n,dx);float det=dot(dx,a);
    d.normalWS=normalize(abs(det)*n-.012*sign(det)*(ddx(height)*a+ddy(height)*b)+n*1e-8);
    s.smoothness=min(s.smoothness,.13);s.occlusion*=1-mud*.24;
}
