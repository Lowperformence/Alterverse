package com.dimensionsfront.ui;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Typeface;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextDirectionHeuristics;
import android.text.TextPaint;
import android.text.TextUtils;
import java.io.ByteArrayOutputStream;

/** Native script shaping and font fallback for the existing Unity IMGUI interface. No network. */
public final class LocalizedTextBitmap {
    private LocalizedTextBitmap() { }
    public static byte[] render(String text, int width, int height, int fontSize,
                                boolean bold, int alignment, boolean wrap) {
        width = Math.max(1, Math.min(2048, width));
        height = Math.max(1, Math.min(512, height));
        TextPaint paint = new TextPaint(TextPaint.ANTI_ALIAS_FLAG | TextPaint.SUBPIXEL_TEXT_FLAG);
        paint.setColor(Color.WHITE);
        paint.setTypeface(Typeface.create("sans-serif", bold ? Typeface.BOLD : Typeface.NORMAL));
        Layout.Alignment horizontal = alignment % 3 == 1 ? Layout.Alignment.ALIGN_CENTER :
            alignment % 3 == 2 ? Layout.Alignment.ALIGN_OPPOSITE : Layout.Alignment.ALIGN_NORMAL;
        StaticLayout layout;
        int size = Math.max(12, fontSize);
        do {
            paint.setTextSize(size);
            layout = StaticLayout.Builder.obtain(text, 0, text.length(), paint, width)
                .setAlignment(horizontal).setIncludePad(false)
                .setTextDirection(TextDirectionHeuristics.FIRSTSTRONG_LTR)
                .setEllipsize(TextUtils.TruncateAt.END).setMaxLines(wrap ? 100 : 1).build();
            if (layout.getHeight() <= height && (wrap || paint.measureText(text) <= width)) break;
            size--;
        } while (size >= 12);
        Bitmap bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
        try {
            Canvas canvas = new Canvas(bitmap);
            float y = alignment / 3 == 1 ? Math.max(0, (height - layout.getHeight()) * .5f) :
                alignment / 3 == 2 ? Math.max(0, height - layout.getHeight()) : 0;
            canvas.translate(0, y);
            layout.draw(canvas);
            ByteArrayOutputStream stream = new ByteArrayOutputStream();
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream);
            return stream.toByteArray();
        } finally { bitmap.recycle(); }
    }
}
