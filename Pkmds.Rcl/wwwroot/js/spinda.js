'use strict';

window.spindaRenderer = {
    // Spot parameters derived directly from pokeos.com's SpindaPatternGenerator CSS layout.
    //
    // Each container is a square (side = cw * 512 px) positioned at (cl*512, ct*512).
    // crot  = container CSS rotation in degrees (compounds with the spot's own rotation).
    // spotW / spotH = spot dimensions as a fraction of the container side length.
    //
    // Byte/nibble mapping (little-endian, from pattern uint32):
    //   bits  0– 7 → spot 0 / leftEar   (lower nibble = X/left, upper nibble = Y/top)
    //   bits  8–15 → spot 1 / rightEar
    //   bits 16–23 → spot 2 / leftFace
    //   bits 24–31 → spot 3 / rightFace
    //
    // Spot top-left inside container = (xDigit/15 * 0.66, yDigit/15 * 0.66) * containerSize.
    // Spot center = top-left + (spotW/2, spotH/2) * containerSize, then rotated by crot around
    // the container's own center.  Ellipse tilt in screen space = crot + rot.
    _spots: [
        {cl: 0.11, ct: 0.04, cw: 0.40, spotW: 0.33, spotH: 0.36, crot: 0, rot: -6}, // leftEar
        {cl: 0.54, ct: 0.13, cw: 0.39, spotW: 0.38, spotH: 0.41, crot: 30, rot: 6}, // rightEar
        {cl: 0.14, ct: 0.31, cw: 0.39, spotW: 0.35, spotH: 0.39, crot: 0, rot: -6}, // leftFace
        {cl: 0.34, ct: 0.35, cw: 0.40, spotW: 0.39, spotH: 0.41, crot: 6, rot: 6}, // rightFace
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

        const fillColor = isShiny ? '#B7C75C' : '#FF3B4F';

        for (let i = 0; i < 4; i++) {
            const s = this._spots[i];

            // Lower nibble of each byte = xDigit (horizontal), upper = yDigit (vertical).
            const xDigit = (pattern >>> (i * 8)) & 0xF;
            const yDigit = (pattern >>> (i * 8 + 4)) & 0xF;

            // Container dimensions and origin in image space.
            const cSize = s.cw * SIZE;
            const contOriginX = s.cl * SIZE;
            const contOriginY = s.ct * SIZE;
            const contCenterX = contOriginX + cSize / 2;
            const contCenterY = contOriginY + cSize / 2;

            // Spot semi-axes (half of the spot's CSS width/height within the container).
            const rx = s.spotW * cSize / 2;
            const ry = s.spotH * cSize / 2;

            // Spot top-left in container local space (pokeos formula: n/15 * 66 %).
            const tlX = (xDigit / 15 * 0.66) * cSize;
            const tlY = (yDigit / 15 * 0.66) * cSize;

            // Spot center in container local space = top-left + half spot size.
            const localX = tlX + rx;
            const localY = tlY + ry;

            // Spot center in image space before container rotation.
            const unrotX = contOriginX + localX;
            const unrotY = contOriginY + localY;

            // Rotate spot center around the container's center by crot (CSS transform compounds).
            const crotRad = s.crot * Math.PI / 180;
            const dx = unrotX - contCenterX;
            const dy = unrotY - contCenterY;
            const spotX = contCenterX + dx * Math.cos(crotRad) - dy * Math.sin(crotRad);
            const spotY = contCenterY + dx * Math.sin(crotRad) + dy * Math.cos(crotRad);

            // Ellipse tilt in screen space = container rotation + spot's own rotation.
            const totalRot = (s.crot + s.rot) * Math.PI / 180;

            sCtx.beginPath();
            sCtx.ellipse(spotX, spotY, rx, ry, totalRot, 0, Math.PI * 2);
            sCtx.fillStyle = fillColor;
            sCtx.fill();
        }

        // Step 3 — clip spots to the head region using the mask.
        sCtx.globalCompositeOperation = 'destination-in';
        sCtx.drawImage(headImg, 0, 0, SIZE, SIZE);
        sCtx.globalCompositeOperation = 'source-over';

        // Step 4 — composite masked spots onto the base with multiply blend,
        // then face and mouth overlays — all matching pokeos mix-blend-multiply.
        ctx.globalCompositeOperation = 'multiply';
        ctx.drawImage(spotsCanvas, 0, 0);
        ctx.drawImage(faceImg, 0, 0, SIZE, SIZE);
        ctx.drawImage(mouthImg, 0, 0, SIZE, SIZE);
        ctx.globalCompositeOperation = 'source-over';
    }
};
