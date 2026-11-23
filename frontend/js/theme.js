// Colour roles, defined once. Every colour on the map must come from here and keep its meaning.
//
//   RED    the model-country hero, hostile or destructive events, and alerts. Nothing else.
//   INK    active and focused information (white).
//   MID    secondary text and inactive but relevant things (gray).
//   DIM    background structure: graticule, borders, inactive land (dark gray).
//   TEAL   sensor coverage zones only (the one cool tint allowed).
//
// Bloc is never shown by colour. It is shown by pattern (see the land layer): East = diagonal hatch,
// West = dot hatch, NonAligned = plain faint fill.

export const INK_RGB = "236,236,233";
export const MID_RGB = "154,157,161";
export const RED_RGB = "255,43,43";
export const TEAL_RGB = "120,190,190";
export const BG = "#060607";

export const INK = `rgb(${INK_RGB})`;
export const MID = `rgb(${MID_RGB})`;
export const RED = `rgb(${RED_RGB})`;

// Dark backing under symbols and text halos, so lines behind them never cross a glyph.
export const BACKING = "rgba(6,6,7,0.82)";
export const HALO = "rgba(6,6,7,0.85)";

export const rgba = (rgb, alpha) => `rgba(${rgb},${alpha})`;

export const FONT = '11px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
export const FONT_SMALL = '10px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
export const FONT_BOLD = '600 11px "Barlow Semi Condensed", "Arial Narrow", sans-serif';
