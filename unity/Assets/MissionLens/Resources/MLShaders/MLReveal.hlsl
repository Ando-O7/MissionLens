// Shared by the terrain and the model's side walls: the opening in which the model spreads out from the
// starting point. Set once for all materials with Shader.SetGlobal*.
#ifndef ML_REVEAL_INCLUDED
#define ML_REVEAL_INCLUDED

float4 _MLReveal;   // x: radius the front has reached (map metres), y: flat level before it arrives, zw: centre (x, z)

// Metres behind the front (positive: already revealed).
float MLFront(float2 xz) { return _MLReveal.x - distance(xz, _MLReveal.zw); }

// Relief rises from the flat level over the 120 m behind the front.
float MLRise(float y, float front) { return lerp(_MLReveal.y, y, smoothstep(0.0, 1.0, saturate(front / 120.0))); }

// Anti-aliased line where f crosses an integer; width in pixels.
float MLLine(float f, float width)
{
    float d = abs(frac(f - 0.5) - 0.5) / max(fwidth(f), 1e-5);
    return saturate(width - d);
}

#endif
