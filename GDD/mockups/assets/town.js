/* GeneLife GDD mockups — procedural town renderer.
 * Pure canvas, no dependencies, seeded so every screenshot is identical.
 * This is a *mockup* renderer: it draws the shape of the intended desktop view,
 * not the real game renderer (which is Godot/C#-side).
 */
(function () {
  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  var PAL = {
    ground: ['#6E7F5B', '#75865F', '#67785A', '#7C8A61'],
    groundDry: ['#8A8A61', '#918E66'],
    road: '#5A5650',
    roadLine: '#B8A97C',
    kerb: '#8E8878',
    roof: { Residential: '#8C4A3F', Office: '#3E5468', School: '#7A6A3A', Store: '#8A6A2E', Civic: '#4B5F52', Shelter: '#5C5A6A' },
    wall: '#C9BFA8',
    tree: '#3E5A38',
    treeLit: '#4E6E42'
  };

  function rect(ctx, x, y, w, h, c) { ctx.fillStyle = c; ctx.fillRect(x, y, w, h); }

  function drawTown(canvas, opt) {
    opt = opt || {};
    var dpr = window.devicePixelRatio || 1;
    var W = canvas.clientWidth, H = canvas.clientHeight;
    canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
    var ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    var T = opt.tile || 18;                 // px per tile
    var rnd = mulberry32(opt.seed || 20261128);
    var cols = Math.ceil(W / T) + 2, rows = Math.ceil(H / T) + 2;
    var ox = (opt.originX || 0) * T, oy = (opt.originY || 0) * T; // camera offset in tiles
    var roadEvery = 12, roadW = 3, lot = roadEvery - roadW; // 9-tile lots visually

    // --- ground -------------------------------------------------------------
    for (var x = 0; x < cols; x++) {
      for (var y = 0; y < rows; y++) {
        var dry = rnd() < 0.18;
        var arr = dry ? PAL.groundDry : PAL.ground;
        rect(ctx, x * T - ox, y * T - oy, T, T, arr[(rnd() * arr.length) | 0]);
      }
    }

    // --- roads --------------------------------------------------------------
    for (var gx = 0; gx < cols + 12; gx += roadEvery) {
      var px = gx * T - ox;
      rect(ctx, px, -oy - T, roadW * T, (rows + 2) * T, PAL.road);
      // centre dashes
      ctx.fillStyle = PAL.roadLine;
      for (var dy = 0; dy < (rows + 2) * roadEvery; dy += 6) {
        ctx.fillRect(px + T * 1.35, dy * T - oy, 1.5, T * 2);
      }
      rect(ctx, px - 1, -oy - T, 1.5, (rows + 2) * T, PAL.kerb);
      rect(ctx, px + roadW * T, -oy - T, 1.5, (rows + 2) * T, PAL.kerb);
    }
    for (var gy = 0; gy < rows + 12; gy += roadEvery) {
      var py = gy * T - oy;
      rect(ctx, -ox - T, py, (cols + 2) * T, roadW * T, PAL.road);
      rect(ctx, -ox - T, py - 1, (cols + 2) * T, 1.5, PAL.kerb);
      rect(ctx, -ox - T, py + roadW * T, (cols + 2) * T, 1.5, PAL.kerb);
    }

    // --- lots & buildings ---------------------------------------------------
    var kinds = ['Residential', 'Residential', 'Residential', 'Residential', 'Office', 'Store', 'School', 'Civic'];
    for (var lx = 0; lx < cols + 12; lx += roadEvery) {
      for (var ly = 0; ly < rows + 12; ly += roadEvery) {
        var bx = (lx + roadW) * T - ox, by = (ly + roadW) * T - oy;
        var bw = lot * T, bh = lot * T;
        if (bx > W || by > H || bx + bw < 0 || by + bh < 0) continue;
        // yard
        rect(ctx, bx, by, bw, bh, '#7E8A64');
        // pick kind from a stable hash of lot coords
        var h = (lx * 73856093 ^ ly * 19349663) >>> 0;
        var kind = kinds[h % kinds.length];
        var inset = T * (0.6 + ((h >> 5) % 10) / 12);
        var fw = bw - inset * 2, fh = bh - inset * (2 + ((h >> 9) % 3));
        // wall + roof (top-down: roof fills the footprint)
        rect(ctx, bx + inset + 2, by + inset + 2, fw, fh, 'rgba(20,24,20,.25)'); // shadow
        rect(ctx, bx + inset, by + inset, fw, fh, PAL.wall);
        rect(ctx, bx + inset + 2, by + inset + 2, fw - 4, fh - 4, PAL.roof[kind]);
        // roof ridge
        ctx.fillStyle = 'rgba(255,255,255,.10)';
        ctx.fillRect(bx + inset + 2, by + inset + fh * 0.46, fw - 4, 1.5);
        // windows
        ctx.fillStyle = 'rgba(255,240,200,.55)';
        for (var wx = bx + inset + T * 0.7; wx < bx + inset + fw - T; wx += T * 1.6) {
          ctx.fillRect(wx, by + inset + fh * 0.2, T * 0.5, T * 0.5);
          ctx.fillRect(wx, by + inset + fh * 0.66, T * 0.5, T * 0.5);
        }
      }
    }

    // --- trees between lots -------------------------------------------------
    for (var i = 0; i < 90; i++) {
      var tx = rnd() * W, ty = rnd() * H;
      var r = T * (0.35 + rnd() * 0.35);
      ctx.beginPath(); ctx.arc(tx, ty, r + 1, 0, 6.283); ctx.fillStyle = 'rgba(20,30,20,.22)'; ctx.fill();
      ctx.beginPath(); ctx.arc(tx, ty, r, 0, 6.283);
      ctx.fillStyle = rnd() < 0.5 ? PAL.tree : PAL.treeLit; ctx.fill();
    }

    // --- genos --------------------------------------------------------------
    var people = opt.people || [];
    people.forEach(function (p) {
      var x = p.x * T - ox, y = p.y * T - oy;
      if (x < -20 || y < -20 || x > W + 20 || y > H + 20) return;
      ctx.beginPath(); ctx.ellipse(x, y + 3, 6, 2.6, 0, 0, 6.283); ctx.fillStyle = 'rgba(15,20,15,.28)'; ctx.fill();
      ctx.beginPath(); ctx.arc(x, y, 5.4, 0, 6.283); ctx.fillStyle = '#F0E6CE'; ctx.fill();
      ctx.lineWidth = 2; ctx.strokeStyle = p.colour || '#C4532A'; ctx.stroke();
      if (p.focus) {
        ctx.beginPath(); ctx.arc(x, y, 13, 0, 6.283);
        ctx.strokeStyle = 'rgba(240,230,206,.75)'; ctx.lineWidth = 1.2; ctx.setLineDash([3, 4]); ctx.stroke(); ctx.setLineDash([]);
      }
      if (p.label) {
        ctx.font = '600 11px "Cascadia Mono", Consolas, monospace';
        var w = ctx.measureText(p.label).width;
        ctx.fillStyle = 'rgba(24,18,14,.82)';
        ctx.fillRect(x - w / 2 - 5, y - 30, w + 10, 16);
        ctx.fillStyle = '#F5F1E8';
        ctx.textAlign = 'center'; ctx.fillText(p.label, x, y - 18.5); ctx.textAlign = 'left';
      }
    });

    // --- atmosphere ---------------------------------------------------------
    if (opt.tint) { ctx.fillStyle = opt.tint; ctx.fillRect(0, 0, W, H); }
    if (opt.vignette !== false) {
      var g = ctx.createRadialGradient(W * 0.5, H * 0.45, Math.min(W, H) * 0.25, W * 0.5, H * 0.45, Math.max(W, H) * 0.75);
      g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, 'rgba(10,12,10,.42)');
      ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    }
  }

  /* Interior cutaway: one floor of a house, roof removed, walls as thick strokes.
     Returns anchor points (in CSS px) so the HTML can tag the furniture. */
  function drawInterior(canvas, opt) {
    opt = opt || {};
    var dpr = window.devicePixelRatio || 1;
    var W = canvas.clientWidth, H = canvas.clientHeight;
    canvas.width = Math.round(W * dpr); canvas.height = Math.round(H * dpr);
    var ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    var rnd = mulberry32(opt.seed || 7);

    // --- outside: garden, dark and quiet ------------------------------------
    rect(ctx, 0, 0, W, H, '#2C3A2E');
    for (var i = 0; i < 900; i++) {
      rect(ctx, rnd() * W, rnd() * H, 2, 2, 'rgba(255,255,255,.025)');
    }
    for (var t = 0; t < 26; t++) {
      ctx.beginPath(); ctx.arc(rnd() * W, rnd() * H, 8 + rnd() * 16, 0, 6.283);
      ctx.fillStyle = 'rgba(20,30,22,.55)'; ctx.fill();
    }

    var padL = opt.padL == null ? 54 : opt.padL;
    var padR = opt.padR == null ? 54 : opt.padR;
    var padT = opt.padT == null ? 54 : opt.padT;
    var padB = opt.padB == null ? 54 : opt.padB;
    var fx = padL, fy = padT, fw = W - padL - padR, fh = H - padT - padB;
    var WT = 12;   // outer wall thickness
    var IW = 9;    // interior wall thickness

    // shadow + floor
    rect(ctx, fx + 8, fy + 10, fw, fh, 'rgba(0,0,0,.4)');
    rect(ctx, fx, fy, fw, fh, '#7A5B3C');
    // floorboards
    for (var py = fy; py < fy + fh; py += 13) {
      rect(ctx, fx, py, fw, 1, 'rgba(40,26,14,.35)');
      for (var px = fx + rnd() * 60; px < fx + fw; px += 110 + rnd() * 70) {
        rect(ctx, px, py, 1, 13, 'rgba(40,26,14,.22)');
      }
    }

    // --- outer walls --------------------------------------------------------
    ctx.strokeStyle = '#E8DCC0'; ctx.lineWidth = WT;
    ctx.strokeRect(fx + WT / 2, fy + WT / 2, fw - WT, fh - WT);
    ctx.strokeStyle = 'rgba(0,0,0,.25)'; ctx.lineWidth = 1;
    ctx.strokeRect(fx + WT, fy + WT, fw - WT * 2, fh - WT * 2);

    // --- interior partitions ------------------------------------------------
    var splitX = fx + fw * 0.44, splitY = fy + fh * 0.56;
    ctx.strokeStyle = '#E8DCC0'; ctx.lineWidth = IW;
    // wall between left rooms and the living room, with a doorway gap
    ctx.beginPath();
    ctx.moveTo(splitX, fy + WT); ctx.lineTo(splitX, fy + fh * 0.44);
    ctx.moveTo(splitX, fy + fh * 0.56); ctx.lineTo(splitX, fy + fh - WT);
    ctx.stroke();
    // wall between bedroom and kitchen
    ctx.beginPath();
    ctx.moveTo(fx + WT, splitY); ctx.lineTo(fx + fw * 0.26, splitY);
    ctx.moveTo(fx + fw * 0.34, splitY); ctx.lineTo(splitX, splitY);
    ctx.stroke();

    // --- doors: a gap in the wall plus a leaf, no swing arcs ---------------
    function door(x, y, w, h) {
      rect(ctx, x, y, w, h, '#7A5B3C');
      ctx.strokeStyle = 'rgba(232,220,192,.55)'; ctx.lineWidth = 1.5;
      ctx.beginPath();
      if (w > h) { ctx.moveTo(x, y + h); ctx.lineTo(x, y + h + Math.min(w, 26)); }
      else { ctx.moveTo(x + w, y); ctx.lineTo(x + w + Math.min(h, 26), y); }
      ctx.stroke();
    }
    door(splitX - IW / 2, fy + fh * 0.44, IW, fh * 0.12);
    door(fx + fw * 0.26, splitY - IW / 2, fw * 0.08, IW);

    // --- furniture ----------------------------------------------------------
    var anchors = {};
    function furn(x, y, w, h, c, label, rot) {
      rect(ctx, x + 5, y + 6, w, h, 'rgba(0,0,0,.3)');
      rect(ctx, x, y, w, h, c);
      ctx.strokeStyle = 'rgba(0,0,0,.18)'; ctx.lineWidth = 1; ctx.strokeRect(x + .5, y + .5, w - 1, h - 1);
      if (label) {
        ctx.font = '600 9px "Cascadia Mono", Consolas, monospace';
        ctx.fillStyle = 'rgba(24,18,14,.5)'; ctx.textAlign = 'center';
        ctx.fillText(label, x + w / 2, y + h / 2 + 3); ctx.textAlign = 'left';
      }
      return { x: x + w / 2, y: y + h / 2 };
    }

    // bedroom (top-left)
    var bw = splitX - fx - WT - 26, bh = splitY - fy - WT - 26;
    anchors.bed = furn(fx + WT + 14, fy + WT + 14, Math.min(190, bw * 0.62), 86, '#C9B79A', 'BED');
    anchors.wardrobe = furn(fx + WT + 14, fy + WT + 122, 74, 62, '#9AA6A0', 'WARDROBE');
    rect(ctx, fx + WT + 110, fy + WT + 118, 96, 66, 'rgba(166,58,90,.16)'); // rug

    // kitchen (bottom-left)
    var ky = splitY + 20;
    anchors.kitchen = furn(fx + WT + 14, ky, 190, 56, '#93A0A4', 'KITCHEN');
    anchors.larder = furn(fx + WT + 22, ky + 78, 78, 54, '#C0B49B', 'LARDER');
    anchors.stove = furn(fx + WT + 122, ky + 78, 62, 54, '#7E7A72', '');

    // living room (right)
    var lx = splitX + 34;
    anchors.sofa = furn(lx + fw * 0.06, fy + 70, Math.min(210, fw * 0.3), 74, '#B8A283', 'SOFA');
    anchors.table = furn(lx + fw * 0.06, fy + 176, Math.min(150, fw * 0.22), 86, '#A98F6E', 'TABLE');
    anchors.hearth = furn(fx + fw - 168, fy + 90, 96, 84, '#8F8577', 'HEARTH');
    anchors.chair = furn(lx + fw * 0.06 + 170, fy + 186, 46, 46, '#9C8B6E', '');
    // rug under the table
    var rgx = lx + fw * 0.04, rgy = fy + 160;
    rect(ctx, rgx, rgy - 6, Math.min(300, fw * 0.38), 120, 'rgba(150,110,70,.22)');

    // --- the genos ----------------------------------------------------------
    var gx = lx + fw * 0.06 + 100, gy = fy + 300;
    ctx.beginPath(); ctx.ellipse(gx, gy + 9, 15, 6, 0, 0, 6.283); ctx.fillStyle = 'rgba(0,0,0,.32)'; ctx.fill();
    ctx.beginPath(); ctx.arc(gx, gy, 13, 0, 6.283); ctx.fillStyle = '#F0E6CE'; ctx.fill();
    ctx.lineWidth = 4; ctx.strokeStyle = '#C4532A'; ctx.stroke();
    anchors.genos = { x: gx, y: gy };

    // --- light --------------------------------------------------------------
    var glow = ctx.createRadialGradient(gx, gy, 20, gx, gy, Math.max(W, H) * 0.75);
    glow.addColorStop(0, 'rgba(255,214,140,.20)');
    glow.addColorStop(0.5, 'rgba(255,214,140,.05)');
    glow.addColorStop(1, 'rgba(10,14,12,.55)');
    ctx.fillStyle = glow; ctx.fillRect(0, 0, W, H);
    // hearth light
    var hg = ctx.createRadialGradient(anchors.hearth.x, anchors.hearth.y, 4, anchors.hearth.x, anchors.hearth.y, 150);
    hg.addColorStop(0, 'rgba(255,180,90,.30)'); hg.addColorStop(1, 'rgba(255,180,90,0)');
    ctx.fillStyle = hg; ctx.fillRect(0, 0, W, H);

    return anchors;
  }

  window.TownMock = { drawTown: drawTown, drawInterior: drawInterior };
})();