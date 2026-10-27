# Physics math: symmetric 3x3 eigen decomposition and inertia tensors

Normative: a port must evaluate in this order to produce the same bits. Both precisions (`F` = binary32, `D` = binary64) use the same code with the type swapped. Everything follows the rules of `evaluation-order.md`: only `+ - * /` and the correctly rounded square root, each rounded in the type's precision, no fused operations, products and sums in the written order, sums left to right.

Types: `SymmetricEigenF/D`, `EigenDecompositionF/D`, `InertiaF/D`, `MassPropertiesF/D`, `PrincipalAxesF/D`, namespace `RecursiveAI.OneMath.Physics`.

## Input checks

- Any NaN or infinite entry, mass, length or offset throws `ArgumentOutOfRangeException` naming the parameter and the field (`matrix.M13 is NaN; a finite value is expected.`). A negative mass or length throws the same type.
- A tensor must be symmetric: all nine entries finite and each mirrored pair within `64 * eps * max|entry|` (`eps` = 2^-52 for D, 2^-23 for F), else `ArgumentException` (`not symmetric`). Functions read the **upper triangle** only (M11 M12 M13 M22 M23 M33); every tensor they return is exactly symmetric.
- `Combine` of an empty span, or of parts whose total mass is zero or not finite, throws `ArgumentException`. Errors for a part name it (`parts[1].Mass`).
- Rotation matrices and quaternions are checked for finiteness only; they are assumed to be rotations and unit quaternions.

## Symmetric eigen decomposition

Cyclic Jacobi (Golub and Van Loan, *Matrix Computations*, section 8.5) with Rutishauser's stable rotation.

1. **Scaling.** `m` = largest absolute upper-triangle entry. If `m == 0` the result is eigenvalues 0 and the identity frame. Otherwise `e` = the binary exponent of `m` (2^e <= m < 2^(e+1), subnormals included), `e1 = e >> 1` (arithmetic shift), `e2 = e - e1`. Every entry becomes `(x * 2^-e1) * 2^-e2` (exact except for entries that underflow, which are negligible against `m`). The scaled matrix has largest entry in [1, 2). The eigenvalues are scaled back by `(x * 2^e1) * 2^e2`. A consequence: scaling the input by a power of two scales the eigenvalues by it and leaves the eigenvectors bit-identical (while nothing leaves the normal range).
2. **Sweeps.** At most 16 sweeps; a sweep rotates the pairs (p,q) = (0,1), (0,2), (1,2) in that order; iteration stops after the first sweep in which no pair is rotated. The eigenvector matrix starts as the identity and is updated in the same rotations.
3. **One pair.** With `apq` the pivot, `g = 2*|apq|`: if `apq == 0` or both `|app| + g == |app|` and `|aqq| + g == |aqq|`, set `apq = aqp = 0` and do not rotate. Otherwise `d = aqq - app`. If `|d| > B * g` (B = 2^60 for D, 2^30 for F) then `t = apq / d`; else `theta = d / (apq*2)`, `t = 1 / (|theta| + sqrt(theta*theta + 1))`, negated when `theta < 0`. Then `c = 1 / sqrt(t*t + 1)`, `s = t*c`, `tau = s / (1 + c)`, `shift = t*apq`, `app = app - shift`, `aqq = aqq + shift`, `apq = aqp = 0`. For the third index `r`: `arp' = arp - s*(arq + tau*arp)`, `arq' = arq + s*(arp - tau*arq)`, mirrored to the symmetric positions. Each row `k` of V: `vkp' = vkp - s*(vkq + tau*vkp)`, `vkq' = vkq + s*(vkp - tau*vkq)`.
4. **Output order.** Eigenvalues ascending, by a stable insertion sort on the indices (0,1,2) with comparison `lambda[order[j]] > lambda[key]`; equal values keep the positions the iteration left them (a diagonal input keeps axis order).
5. **Signs.** For the first two eigenvectors the component of largest magnitude is made positive (the lowest index wins a tie; a flip is `0 - x`, so zeros stay +0). The third eigenvector is flipped if `dot(cross(c0, c1), c2) < 0`, so the frame is right-handed (determinant +1). The third vector therefore need not have a positive largest component.

### Accuracy

Stated bound, with `u` = 2^-53 (D) or 2^-24 (F) and `A` the input with `V` its eigenvector matrix and `L` the eigenvalues:

- `max |V L V^T - A| <= 16 u max|A|`
- `max |V^T V - I| <= 10 u` and `|det V - 1| <= 10 u`

It holds when the eigenvalues are in the normal range; a result that lands in the subnormal range is quantized by the format itself (a few subnormal steps are added to the first bound). By Weyl's inequality the first bound also bounds each eigenvalue's absolute error by about `3 * 16 u max|A|`. Eigenvalues that overflow the type become infinite (only possible when the largest entry is within a factor of about 3 of the type's maximum).

Measured over 10^6 random matrices per precision (random, wide dynamic range, rank one and two, repeated and nearly repeated eigenvalues, near-singular spreads, diagonal with repeats, small integers, binary scales across the whole exponent range) against a double-double oracle: D worst 12.4 u reconstruction and 7.5 u orthonormality; F worst 10.4 u and 6.2 u.

## Inertia

Tensors are row-major `Matrix3x3`; offsets, centres and tensors are in one frame.

- **Rotate** `R I R^T`: `S` = the symmetric matrix from the upper triangle, `M = R * S` (the matrix product of `evaluation-order.md`), entry `(i, j)` for `i <= j` = `Dot(row i of M, row j of R)`; the lower triangle mirrors the upper. A quaternion first becomes `q.ToMatrix()` (unit quaternion assumed).
- **ShiftToPoint** (centre of mass to a point at `d` = point minus centre; `m` the mass): `xx = dy*dy + dz*dz`, `yy = dx*dx + dz*dz`, `zz = dx*dx + dy*dy`, `mx = m*dx`, `my = m*dy`; `I11 + m*xx`, `I22 + m*yy`, `I33 + m*zz`, `I12 - mx*dy`, `I13 - mx*dz`, `I23 - my*dz`. **ShiftToCenter** subtracts the same terms on the diagonal and adds them off it. A round trip is exact only up to rounding.
- **Combine** parts (mass `m_i`, centre `c_i`, tensor about `c_i`): in span order, `M = ((0 + m_0) + m_1) + ...`, `S = Σ m_i * c_i` per component the same way, `C = S / M` per component; each tensor entry of the result is the left-to-right sum, from 0, of `ShiftToPoint(I_i, m_i, c_i - C)`. The result is the tensor about `C` with mass `M`.
- **PrincipalAxes**: the eigen decomposition above; `Moments` = eigenvalues, `Rotation` = eigenvector matrix (`I = Rotation * diag(Moments) * Rotation^T`), `Orientation` = `Quaternion.FromMatrix(Rotation)`.
- **Solids** (axes along the shape; `h` half extents): box diagonal `(m*(hy*hy + hz*hz))/3`, `(m*(hx*hx + hz*hz))/3`, `(m*(hx*hx + hy*hy))/3`; sphere `((2*m)*(r*r))/5`; cylinder along Y, `rr = r*r`, `hq = hh*hh` for half height `hh`: Ixx = Izz = `(m*(3*rr + 4*hq))/12`, Iyy = `(m*rr)/2`.

## Tests and hash-matrix scenarios

`tests/OneMath.Tests/Physics/` holds known-answer tests (diagonal, 2- and 3-fold repeats, rank-deficient, tridiagonal, extreme scales, power-of-two commutation, hostile input), the 10^6-case oracle sweeps per precision (`ONEMATH_PHYSICS_CASES` overrides the count) and the inertia identities. The scenarios `physics/{f32,f64}/{eigen,inertia}` are in the hash matrix.
