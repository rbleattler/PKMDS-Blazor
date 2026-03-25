'use strict';

window.spindaRenderer = {
    // Spot container definitions ported from SpindaPatternPlugin (SpindaPatternForm.cs).
    //
    // The nibble in each byte controls position within a container region (0-66 % of
    // the container's width/height). Some containers are also coordinate-rotated so
    // ear spots follow the angled anatomy.
    //
    // Nibble-to-spot mapping (plugin's hex-string order, least-significant first):
    //   bits  0- 7  → leftEar  (hex digits 7-6)
    //   bits  8-15  → rightEar (hex digits 5-4)
    //   bits 16-23  → leftFace (hex digits 3-2)
    //   bits 24-31  → rightFace(hex digits 1-0)
    //
    // Each entry: cl/ct = container left/top (fraction of image size)
    //             cw/ch = container width/height (fraction of image size)
    //             sw/sh = spot width/height (fraction of container size)
    //             rot   = spot ellipse tilt in degrees
    //             crot  = container coordinate rotation in degrees
    _spots: [
        { cl: 0.17, ct: 0.12, cw: 0.40, ch: 0.40, sw: 0.33, sh: 0.36, rot: -6, crot:  0 }, // leftEar
        { cl: 0.57, ct: 0.24, cw: 0.39, ch: 0.39, sw: 0.38, sh: 0.41, rot:  6, crot: 30 }, // rightEar
        { cl: 0.20, ct: 0.39, cw: 0.39, ch: 0.39, sw: 0.35, sh: 0.39, rot: -6, crot:  0 }, // leftFace
        { cl: 0.40, ct: 0.43, cw: 0.40, ch: 0.40, sw: 0.39, sh: 0.41, rot:  6, crot:  6 }, // rightFace
    ],

    _loadImage: function (src) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => reject(new Error('Failed to load: ' + src));
            img.src = src;
        });
    },

    render: async function (canvas, pattern, isShiny, baseUrl, headUrl, faceUrl, mouthUrl) {
        const SIZE = 512;
        canvas.width = SIZE;
        canvas.height = SIZE;
        const ctx = canvas.getContext('2d');

        const [baseImg, headImg, faceImg, mouthImg] = await Promise.all([
            this._loadImage(baseUrl),
            this._loadImage(headUrl),
            this._loadImage(faceUrl),
            this._loadImage(mouthUrl),
        ]);

        // Step 1 — draw the spotless base sprite.
        ctx.clearRect(0, 0, SIZE, SIZE);
        ctx.drawImage(baseImg, 0, 0, SIZE, SIZE);

        // Step 2 — build the spots layer on an off-screen canvas.
        const spotsCanvas = document.createElement('canvas');
        spotsCanvas.width = SIZE;
        spotsCanvas.height = SIZE;
        const sCtx = spotsCanvas.getContext('2d');

        const fillColor   = isShiny ? '#B7C75C' : '#FF3B4F';
        const strokeColor = isShiny ? '#96AA46' : '#DC2840';

        for (let i = 0; i < 4; i++) {
            const s = this._spots[i];

            // Lower nibble of each byte = xDigit (horizontal), upper = yDigit (vertical).
            const xDigit = (pattern >>> (i * 8))     & 0xF;
            const yDigit = (pattern >>> (i * 8 + 4)) & 0xF;

            // Container bounds in pixels.
            const containerX = s.cl * SIZE;
            const containerY = s.ct * SIZE;
            const containerW = s.cw * SIZE;
            const containerH = s.ch * SIZE;

            // Position within the container: nibble maps 0-15 → 0-66 % of container.
            const rawX = ((xDigit / 15) * 0.66) * containerW;
            const rawY = ((yDigit / 15) * 0.66) * containerH;

            // Apply optional container-coordinate rotation around the container centre.
            let spotX, spotY;
            if (Math.abs(s.crot) > 0.01) {
                const ccX = containerW / 2;
                const ccY = containerH / 2;
                const relX = rawX - ccX;
                const relY = rawY - ccY;
                const rad = s.crot * Math.PI / 180;
                spotX = containerX + ccX + relX * Math.cos(rad) - relY * Math.sin(rad);
                spotY = containerY + ccY + relX * Math.sin(rad) + relY * Math.cos(rad);
            } else {
                spotX = containerX + rawX;
                spotY = containerY + rawY;
            }

            const spotW = containerW * s.sw;
            const spotH = containerH * s.sh;

            // Draw the spot as a rotated, filled ellipse with a thin border.
            sCtx.beginPath();
            sCtx.ellipse(spotX, spotY, spotW / 2, spotH / 2, s.rot * Math.PI / 180, 0, Math.PI * 2);
            sCtx.fillStyle = fillColor;
            sCtx.fill();
            sCtx.strokeStyle = strokeColor;
            sCtx.lineWidth = 1;
            sCtx.stroke();
        }

        // Step 3 — clip spots to the head region using the mask.
        sCtx.globalCompositeOperation = 'destination-in';
        sCtx.drawImage(headImg, 0, 0, SIZE, SIZE);
        sCtx.globalCompositeOperation = 'source-over';

        // Step 4 — composite masked spots onto the base.
        ctx.drawImage(spotsCanvas, 0, 0);

        // Step 5 — draw face and mouth overlays on top.
        ctx.drawImage(faceImg, 0, 0, SIZE, SIZE);
        ctx.drawImage(mouthImg, 0, 0, SIZE, SIZE);
    }
};
