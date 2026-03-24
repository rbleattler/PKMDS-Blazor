'use strict';

window.spindaRenderer = {
    // Base spot centers and semi-axis radii on the 512×512 Home sprite.
    // Each spot moves 0–15 px from its base position (one nibble per axis).
    // The head-mask PNG clips any overflow to the head region.
    _spots: [
        { cx: 208, cy: 172, rx: 50, ry: 46 }, // top-left (near left ear)
        { cx: 298, cy: 162, rx: 50, ry: 46 }, // top-right (near right ear)
        { cx: 186, cy: 284, rx: 56, ry: 50 }, // bottom-left (body)
        { cx: 320, cy: 278, rx: 56, ry: 50 }, // bottom-right (body)
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

        const outerColor = isShiny ? '#B7C75C' : '#FF3B4F';
        const innerColor = isShiny ? '#96AA46' : '#DC2840';

        for (let i = 0; i < 4; i++) {
            const base = this._spots[i];

            // Each spot gets its x-offset from bits [8i .. 8i+3] and
            // its y-offset from bits [8i+4 .. 8i+7] of the pattern.
            const nibbleX = (pattern >>> (i * 8)) & 0xF;
            const nibbleY = (pattern >>> (i * 8 + 4)) & 0xF;
            const cx = base.cx + nibbleX;
            const cy = base.cy + nibbleY;

            // Outer ellipse.
            sCtx.beginPath();
            sCtx.ellipse(cx, cy, base.rx, base.ry, 0, 0, Math.PI * 2);
            sCtx.fillStyle = outerColor;
            sCtx.fill();

            // Inner ellipse — slightly smaller and offset for a shaded look.
            sCtx.beginPath();
            sCtx.ellipse(cx - 4, cy - 4, base.rx * 0.60, base.ry * 0.60, 0, 0, Math.PI * 2);
            sCtx.fillStyle = innerColor;
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
