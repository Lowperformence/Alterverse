// Original direction-space stars/nebula baker for the shipped Unity panorama.
// Star cores are large enough to survive 2048/1024 mobile texture reduction.
// All background queries use unit world directions; there is no flat-screen UV skybox.
precision highp float;
const float PI=3.141592653589793;
vec3 sun=normalize(vec3(-.45,.65,-.60));
float h31(vec3 p){p=fract(p*.1031);p+=dot(p,p.yzx+33.33);return fract((p.x+p.y)*p.z);}
vec3 h33(vec3 p){return vec3(h31(p),h31(p+17.31),h31(p+63.77));}
float noise3(vec3 p){vec3 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);
 return mix(mix(mix(h31(i),h31(i+vec3(1,0,0)),f.x),mix(h31(i+vec3(0,1,0)),h31(i+vec3(1,1,0)),f.x),f.y),
 mix(mix(h31(i+vec3(0,0,1)),h31(i+vec3(1,0,1)),f.x),mix(h31(i+vec3(0,1,1)),h31(i+vec3(1,1,1)),f.x),f.y),f.z);}
float fbm(vec3 p){float sum=0.,a=.53;for(int i=0;i<5;i++){sum+=a*noise3(p);p=p*2.07+vec3(11.2,7.7,3.1);a*=.49;}return sum;}
vec3 background(vec3 d){
 float lane=exp(-pow((d.y-d.x*.34+sin(d.z*3.)*.08)*3.0,2.));
 vec3 warp=vec3(fbm(d*2.2+4.),fbm(d*2.2+28.),fbm(d*2.2+61.));
 float cloud=fbm(d*4.+warp*2.4+17.);
 float dust=pow(max(0.,cloud-.24),2.1)*lane;
 float filament=pow(1.-abs(noise3(d*21.+warp*5.)*2.-1.),9.);
 float tint=smoothstep(-.65,.65,d.x+.35*d.z);
 vec3 color=vec3(.0018,.0032,.008);
 color+=mix(vec3(.30,.075,.48),vec3(.045,.31,.40),tint)*dust*.78;
 color+=vec3(.10,.12,.21)*filament*dust*.30;
 color+=vec3(.028,.022,.040)*lane*pow(cloud,3.);
 // Stars are fixed 3D points on a thin spherical shell. Neighbour sampling
 // preserves continuity across grid cells as well as panorama/cube boundaries.
 const float scale=110.;vec3 cell=floor(d*scale);
 for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
  vec3 key=cell+vec3(float(x),float(y),float(z));
  float gate=h31(key+137.);
  if(gate<.94)continue;
  vec3 point=key+.18+.64*h33(key+9.);
  if(abs(length(point)-scale)>.40)continue;
  vec3 dir=normalize(point);float distance=length(d-dir);
  float radius=mix(.0017,.0043,pow(h31(key+83.),2.5));
  float core=exp(-distance*distance/(radius*radius)*2.2);
  float halo=exp(-distance*distance/(radius*radius)*.20)*.035;
  vec3 star=mix(vec3(.58,.73,1.),vec3(1.,.83,.64),h31(key+51.));
  color+=star*(core+halo)*mix(.50,2.2,pow(h31(key+7.),1.9))*(.85+lane*.4);
 }
 return color;
}
