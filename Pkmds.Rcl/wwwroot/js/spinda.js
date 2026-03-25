'use strict';

window.spindaRenderer = {
    // Spot center ranges for the 512×512 HOME sprite.
    //
    // Derived by anchoring each spot to the corresponding head-mask blob centroid
    // at mid-range nibble (digit ≈ 8), then using the SpindaPatternPlugin container
    // width (≈ 39–40 % of image × 66 % travel factor) as the movement range.
    //
    // Blob centroids (from alpha-channel analysis of 327-head.png):
    //   leftEar  (184, 148)  rightEar (373, 176)
    //   leftFace (214, 284)  rightFace(319, 275)
    //
    // Nibble-to-spot byte mapping (little-endian):
    //   bits  0- 7 → spot 0 / left ear
    //   bits  8-15 → spot 1 / right ear
    //   bits 16-23 → spot 2 / left face
    //   bits 24-31 → spot 3 / right face
    //
    // Within each byte: lower nibble = X (horizontal), upper nibble = Y (vertical).
    // spotX = xMin + (digit / 15) * (xMax - xMin); similarly for Y.
    // rx / ry = ellipse semi-axes (px);  rot = ellipse tilt (degrees).
    _spots: [
        { xMin: 112, xMax: 247, yMin:  76, yMax: 211, rx: 34, ry: 37, rot: -6 }, // leftEar
        { xMin: 303, xMax: 434, yMin: 106, yMax: 238, rx: 38, ry: 41, rot:  6 }, // rightEar
        { xMin: 144, xMax: 276, yMin: 214, yMax: 345, rx: 35, ry: 39, rot: -6 }, // leftFace
        { xMin: 247, xMax: 383, yMin: 203, yMax: 338, rx: 40, ry: 42, rot:  6 }, // rightFace
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

            // Map each nibble (0–15) linearly over the spot's center range.
            const spotX = s.xMin + (xDigit / 15) * (s.xMax - s.xMin);
            const spotY = s.yMin + (yDigit / 15) * (s.yMax - s.yMin);

            // Draw as a rotated, filled ellipse with a thin border.
            sCtx.beginPath();
            sCtx.ellipse(spotX, spotY, s.rx, s.ry, s.rot * Math.PI / 180, 0, Math.PI * 2);
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
