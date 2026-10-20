#!/usr/bin/env python3
"""Verify the map shader's bounded geological-gradient math using the standard library.

Reads the coefficients and source contracts from the production shaders, then
checks analytical tangent-gradient reconstruction on 4,096 deterministic random
triangles. This is numerical/source validation, not HLSL compilation, Unity/URP
integration, visual rendering, or Android GPU/performance validation.
"""
import argparse
import json
import math
from pathlib import Path
import random
import re


ROOT = Path(__file__).resolve().parents[3]
NUMBER = r"([0-9]*\.?[0-9]+)"


def extract(source, pattern):
    match = re.search(pattern, source)
    if match is None:
        raise AssertionError("Production shader formula changed: " + pattern)
    return tuple(float(value) for value in match.groups())


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def mul(a, scalar):
    return tuple(x * scalar for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def length(a):
    return math.sqrt(dot(a, a))


def unit(a):
    return mul(a, 1 / length(a))


def visibility(footprint, fade_start, fade_end):
    t = max(0.0, min(1.0, (footprint - fade_start) / (fade_end - fade_start)))
    return 1 - t * t * (3 - 2 * t)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Optional JSON report path")
    args = parser.parse_args()
    planet = (ROOT / "Assets/Resources/DF_PlanetSurface.shader").read_text()
    scenery = (ROOT / "Assets/Resources/DF_ScenerySurface.shader").read_text()
    micro = (ROOT / "Assets/Resources/DF_SurfaceMicro.hlsl").read_text()

    # Bind the model to the actual checked-in formula, not independent constants.
    cap, length_epsilon = extract(planet, r"geoGradient\*=min\(1\.0," + NUMBER + r"/max\(length\(geoGradient\)," + NUMBER + r"\)\)")
    (det_epsilon,) = extract(planet, r"/max\(abs\(determinant\)," + NUMBER + r"\)")
    ground_relief, stone_relief = extract(planet, r"broadRelief=geologicalContrast\*lerp\(" + NUMBER + r"h," + NUMBER + r"h,")
    height_r, height_a = extract(planet, r"geoHeight=geology\.r\*" + NUMBER + r"\+geology\.a\*" + NUMBER)
    (macro_strength,) = extract(planet, r'_MacroStrength\("Geological contrast", Float\) = ' + NUMBER)
    fade_start, fade_end = extract(micro, r"return 1-smoothstep\(" + NUMBER + r"," + NUMBER + r",footprint\)")
    assert abs(height_r + height_a - 1) < 1e-12
    assert 0 < det_epsilon and 0 < length_epsilon < cap < 1
    assert 0 < ground_relief <= stone_relief and fade_start < fade_end

    for formula in (
        "float3 dpdx=ddx(p),dpdy=ddy(p);",
        "float3 tangentX=cross(dpdy,n),tangentY=cross(n,dpdx);",
        "float determinant=dot(dpdx,tangentX);",
        "(determinant<0?-1.0:1.0)",
        "(ddx(geoHeight)*tangentX+ddy(geoHeight)*tangentY)*inverseDeterminant",
        "gradient+=geoGradient*(broadRelief*geoVisibility);",
    ):
        assert formula in planet, "Production gradient formula changed: " + formula
    for source in (planet, scenery):
        assert source.count("CBUFFER_START(UnityPerMaterial)") == 1
        assert source.count("{") == source.count("}")
        for name in ("ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals"):
            assert 'Name "' + name + '"' in source
    assert "geoX*geoWeights.x+geology*geoWeights.y+geoZ*geoWeights.z" in planet

    rng = random.Random(2048)
    max_error = 0.0
    bound = cap * macro_strength * stone_relief
    footprints = (0, fade_start, (fade_start + fade_end) / 2, fade_end, 1)
    for case in range(4096):
        normal = unit(tuple(rng.gauss(0, 1) for _ in range(3)))
        tangent = tuple(rng.gauss(0, 1) for _ in range(3))
        tangent = unit(add(tangent, mul(normal, -dot(tangent, normal))))
        bitangent = cross(normal, tangent)
        sx = 10 ** rng.uniform(-4, 1)
        sy = 10 ** rng.uniform(-4, 1)
        if case % 2:
            sx = -sx
        dpdx, dpdy = mul(tangent, sx), mul(bitangent, sy)
        tangent_x, tangent_y = cross(dpdy, normal), cross(normal, dpdx)
        determinant = dot(dpdx, tangent_x)
        analytic = add(mul(tangent, rng.uniform(-2, 2)), mul(bitangent, rng.uniform(-2, 2)))
        hx, hy = dot(analytic, dpdx), dot(analytic, dpdy)
        inverse = (-1 if determinant < 0 else 1) / max(abs(determinant), det_epsilon)
        gradient = mul(add(mul(tangent_x, hx), mul(tangent_y, hy)), inverse)
        gradient = mul(gradient, min(1, cap / max(length(gradient), length_epsilon)))
        assert all(math.isfinite(x) for x in gradient)
        assert length(gradient) <= cap + 1e-10
        assert abs(dot(gradient, normal)) < 1e-8
        if abs(determinant) >= det_epsilon:
            expected = mul(analytic, min(1, cap / max(length(analytic), length_epsilon)))
            error = length(add(expected, mul(gradient, -1)))
            assert error < 1e-8
            max_error = max(max_error, error)
        neutral = mul(add(mul(tangent_x, 0), mul(tangent_y, 0)), inverse)
        assert length(neutral) == 0
        assert length(mul(gradient, 0)) == 0  # Macro strength zero disables broad relief.
        for footprint in footprints:
            final = mul(gradient, macro_strength * stone_relief * visibility(footprint, fade_start, fade_end))
            assert all(math.isfinite(x) for x in final)
            assert length(final) <= bound + 1e-10
            if footprint >= fade_end:
                assert length(final) == 0

    report = {
        "geological_gradient_orientation_cases": 4096,
        "pixel_footprint_cases": 4096 * len(footprints),
        "maximum_tangent_gradient": cap,
        "maximum_final_normal_perturbation_at_default_strength": bound,
        "analytical_reconstruction_max_error": max_error,
        "neutral_data_zero_relief": True,
        "grazing_triangle_values_finite": True,
        "scope": "Numerical formula verification and source contract inspection only; no HLSL compilation, Unity/URP integration, rendering or Android performance test.",
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
