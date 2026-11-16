// Added after the original character palette/pattern shading. Never alters the source atlas.
float EndingWearHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float EndingWearNoise(float2 p)
{
    float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);
    return lerp(lerp(EndingWearHash(a),EndingWearHash(a+float2(1,0)),b.x),lerp(EndingWearHash(a+float2(0,1)),EndingWearHash(a+1),b.x),b.y);
}
float EndingWearGrain(float2 p) { return EndingWearNoise(p)*.58+EndingWearNoise(p*2.11+12)*.28+EndingWearNoise(p*4.23+37)*.14; }
float EndingWearSpots(float2 p)
{
    float2 cell=floor(p),f=frac(p);float seed=EndingWearHash(cell+2.7);
    float2 center=.18+.64*float2(EndingWearHash(cell+19),EndingWearHash(cell+61));
    float radius=lerp(.025,.31,seed*seed);
    return (1-smoothstep(radius*.55,radius,length((f-center)*float2(1,.85))))*step(.43,EndingWearHash(cell+42));
}
void ApplyFailureCostumeWear(inout SurfaceData s,inout InputData d,float3 local,float2 uv)
{
    float2 p=(local.xy+local.z*float2(.47,.83))*3.2+1.7;
    float coarse=EndingWearGrain(p*2.4),grit=EndingWearGrain(p*25),fine=EndingWearNoise(p*55);
    float grime=smoothstep(.28,.73,coarse);
    float weave=sin(uv.x*1700)*sin(uv.y*1700);
    half3 raw=s.albedo*(.82+.18*grit+weave*.025);
    raw=lerp(raw,raw*half3(.28,.23,.17),grime*.63);
    float cluster=EndingWearGrain(p*2.1+float2(27,8));
    float blob=smoothstep(.48,.66,cluster+EndingWearNoise(p*23)*.13);
    float drops=max(EndingWearSpots(p*7.3),EndingWearSpots(p*19.7+17)*.55);
    float drip=smoothstep(.75,.88,EndingWearNoise(p*float2(45,1.9)+11))*smoothstep(.42,.62,cluster);
    float stain=saturate(max(blob,max(drops*.82,drip*.85))*.93);
    stain*=lerp(.55,1,smoothstep(.24,.37,EndingWearNoise(p*155)));
    half3 dried=lerp(half3(.035,.006,.005),half3(.16,.025,.017),EndingWearGrain(p*12+4));
    s.albedo=lerp(raw,dried,stain*.98);
    float height=grit*.06+fine*.005+weave*.0004+stain*.018;
    float3 n=d.normalWS,dx=ddx(d.positionWS),dy=ddy(d.positionWS);
    float3 r1=cross(dy,n),r2=cross(n,dx);float determinant=dot(dx,r1);
    d.normalWS=normalize(abs(determinant)*n-.018*sign(determinant)*(ddx(height)*r1+ddy(height)*r2)+n*1e-8);
    s.smoothness=lerp(.055,.14,stain);s.occlusion=1-grime*.22;
}
