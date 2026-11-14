// Shared by the terrain and the model's side walls: the opening in which the model spreads out from the
// starting point. Set once for all materials with Shader.SetGlobal*.
#ifndef ML_REVEAL_INCLUDED
#define ML_REVEAL_INCLUDED

float4 _MLReveal;   // x: radius the front has reached (map metres), y: flat level before it arrives, zw: centre (x, z)

// The tabletop model shows the part of the terrain that lies over the table. _MLTableInv takes a world position
// into the table's own frame (metres; x across, z away from the person); _MLTableClip.xy are the half sizes of
// what is shown. Away from the table the half sizes are made huge, so nothing is cut.
float4x4 _MLTableInv;
float4 _MLTableClip;
float4 _MLLens;     // the lens on the table: xy its centre (table metres), z its radius, w how much it is on (0..1)
float2 MLTablePos(float3 positionWS) { return mul(_MLTableInv, float4(positionWS, 1.0)).xz; }
void MLTableCut(float2 t) { clip(_MLTableClip.x - abs(t.x)); clip(_MLTableClip.y - abs(t.y)); }

// Metres behind the front (positive: already revealed).
float MLFront(float2 xz) { return _MLReveal.x - distance(xz, _MLReveal.zw); }

// Relief rises from the flat level over the 120 m behind the front.
float MLRise(float y, float front) { return lerp(_MLReveal.y, y, smoothstep(0.0, 1.0, saturate(front / 120.0))); }

// Anti-aliased line where f crosses an integer; width in pixels.
float MLLine(float f, float width)
{
    float w = max(fwidth(f), 1e-5), d = abs(frac(f - 0.5) - 0.5) / w;
    return saturate(width - d) * saturate(2.0 - w * 6.0);   // gone where the lines would lie closer than a few pixels
}

#endif
