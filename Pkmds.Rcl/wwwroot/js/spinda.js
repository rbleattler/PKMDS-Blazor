'use strict';

window.spindaRenderer = {
    // Base spot centers and semi-axis radii on the 512×512 Home sprite.
    // Each spot moves 0–15 px from its base position (one nibble per axis).
    // The head-mask PNG clips any overflow to the head region.
    // Base positions derived from actual pixel analysis of 327-head.png.
    // The mask has 4 opaque blobs; their centroids at nibble=8 give the target
    // center, so base = centroid − 8. Radii are ~60% of each blob's fill radius.
    //   top-left  blob centroid (184,148) → base (176,140), bounds x=121-280 y=66-216
    //   top-right blob centroid (373,176) → base (365,168), bounds x=281-439 y=122-216
    //   bot-left  blob centroid (214,284) → base (206,276), bounds x=133-280 y=217-366
    //   bot-right blob centroid (319,275) → base (311,267), bounds x=281-411 y=217-365
    _spots: [
        { cx: 176, cy: 140, rx: 44, ry: 42 }, // spot 0: left ear
        { cx: 365, cy: 168, rx: 40, ry: 38 }, // spot 1: right ear
        { cx: 206, cy: 276, rx: 52, ry: 50 }, // spot 2: left eye (largest blob)
        { cx: 311, cy: 267, rx: 40, ry: 38 }, // spot 3: right eye
    ],

    /** Load an image from a URL; returns Promise<HTMLImageElement>. */
    _loadImage: function (src) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => reject(new Error('Failed to load: ' + src));
            img.src = src;
        });
    },

    /**
     * Composite a Spinda spot pattern onto a canvas element.
     *
     * @param {HTMLCanvasElement} canvas   Target canvas (already in the DOM).
     * @param {number}  pattern    32-bit unsigned integer, already endian-corrected by C#.
     * @param {boolean} isShiny
     * @param {string}  baseUrl    Path to the spotless base sprite.
     * @param {string}  headUrl    Path to the head alpha-mask PNG.
     * @param {string}  faceUrl    Path to the face overlay PNG.
     * @param {string}  mouthUrl   Path to the mouth overlay PNG.
     */
    render: async function (canvas, pattern, isShiny, baseUrl, headUrl, faceUrl, mouthUrl) {
        const SIZE = 512;
        canvas.width = SIZE;
        canvas.height = SIZE;

        const ctx = canvas.getContext('2d');

        // Load all four images in parallel.
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

        const spotColor = isShiny ? '#B7C75C' : '#DC2840';

        for (let i = 0; i < 4; i++) {
            const base = this._spots[i];

            // Each spot gets its x-offset from bits [8i .. 8i+3] and
            // its y-offset from bits [8i+4 .. 8i+7] of the pattern.
            const nibbleX = (pattern >>> (i * 8)) & 0xF;
            const nibbleY = (pattern >>> (i * 8 + 4)) & 0xF;
            const cx = base.cx + nibbleX;
            const cy = base.cy + nibbleY;

            sCtx.beginPath();
            sCtx.ellipse(cx, cy, base.rx, base.ry, 0, 0, Math.PI * 2);
            sCtx.fillStyle = spotColor;
            sCtx.fill();
        }

        // Step 3 — apply the head mask so spots are clipped to the head region.
        // 'destination-in' preserves spots only where the mask is opaque.
        sCtx.globalCompositeOperation = 'destination-in';
        sCtx.drawImage(headImg, 0, 0, SIZE, SIZE);
        sCtx.globalCompositeOperation = 'source-over';

        // Step 4 — composite the masked spot layer onto the base.
        ctx.drawImage(spotsCanvas, 0, 0);

        // Step 5 — draw face and mouth overlays on top.
        ctx.drawImage(faceImg, 0, 0, SIZE, SIZE);
        ctx.drawImage(mouthImg, 0, 0, SIZE, SIZE);
    }
};
