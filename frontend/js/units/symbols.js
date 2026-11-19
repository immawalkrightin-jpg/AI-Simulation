export function drawSymbol(ctx, kind, x, y, r) {
  ctx.beginPath();
  switch (kind) {
    case "Artillery":
      ctx.arc(x, y, r * 0.7, 0, Math.PI * 2);
      ctx.moveTo(x, y);
      ctx.lineTo(x + r * 0.9, y - r * 1.3);
      break;
    case "Armor":
      ctx.rect(x - r, y - r * 0.6, r * 2, r * 1.2);
      ctx.moveTo(x - r * 0.6, y);
      ctx.ellipse(x, y, r * 0.6, r * 0.3, 0, 0, Math.PI * 2);
      break;
    case "SupplyConvoy":
      ctx.moveTo(x, y - r);
      ctx.lineTo(x + r, y);
      ctx.lineTo(x, y + r);
      ctx.lineTo(x - r, y);
      ctx.closePath();
      break;
    case "Naval":
      ctx.moveTo(x - r, y - r * 0.4);
      ctx.lineTo(x + r, y - r * 0.4);
      ctx.lineTo(x + r * 0.5, y + r * 0.6);
      ctx.lineTo(x - r * 0.5, y + r * 0.6);
      ctx.closePath();
      break;
    case "Air":
      ctx.moveTo(x - r, y + r * 0.6);
      ctx.lineTo(x, y - r * 0.8);
      ctx.lineTo(x + r, y + r * 0.6);
      break;
    case "Infantry":
      ctx.rect(x - r * 0.8, y - r * 0.8, r * 1.6, r * 1.6);
      ctx.moveTo(x - r * 0.8, y - r * 0.8);
      ctx.lineTo(x + r * 0.8, y + r * 0.8);
      ctx.moveTo(x + r * 0.8, y - r * 0.8);
      ctx.lineTo(x - r * 0.8, y + r * 0.8);
      break;
    default:
      ctx.arc(x, y, r * 0.7, 0, Math.PI * 2);
  }
  ctx.stroke();
}
