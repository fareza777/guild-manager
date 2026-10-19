package com.pixelgamemaster.emberhall;

import android.app.Activity;
import android.os.Build;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import com.google.android.gms.ads.AdView;
import java.util.WeakHashMap;

/** Keep only the game's banner above Android navigation, including temporarily hidden system bars. */
public final class EmberhallNavigation {
    private static final WeakHashMap<View, Boolean> watched = new WeakHashMap<>();
    private static volatile int[] metrics = new int[7];

    public static int[] getMetrics() { return metrics.clone(); }

    public static void refresh(Activity activity) {
        activity.runOnUiThread(() -> {
            View decor = activity.getWindow().getDecorView();
            View content = activity.findViewById(android.R.id.content);
            WindowInsets insets = decor.getRootWindowInsets();
            if (content == null || insets == null || content.getHeight() == 0) return;
            int nav = Build.VERSION.SDK_INT >= 30
                ? insets.getInsetsIgnoringVisibility(WindowInsets.Type.navigationBars()).bottom
                : insets.getStableInsetBottom();
            int cutout = Build.VERSION.SDK_INT >= 28 && insets.getDisplayCutout() != null
                ? insets.getDisplayCutout().getSafeInsetBottom() : 0;
            int[] decorAt = new int[2], contentAt = new int[2];
            decor.getLocationOnScreen(decorAt); content.getLocationOnScreen(contentAt);
            int windowBottom = decorAt[1] + decor.getHeight();
            int alreadyExcluded = Math.max(0, windowBottom - contentAt[1] - content.getHeight());
            int gap = Math.round(8 * activity.getResources().getDisplayMetrics().density);
            int margin = Math.max(0, Math.max(nav, cutout) - alreadyExcluded) + gap;
            int[] bannerBounds = new int[] {-1, -1};
            place(content, activity, margin, bannerBounds);
            boolean visible = Build.VERSION.SDK_INT >= 30
                ? insets.isVisible(WindowInsets.Type.navigationBars()) : insets.getSystemWindowInsetBottom() > 0;
            metrics = new int[] {1, margin, nav, visible ? 1 : 0, bannerBounds[0], bannerBounds[1], windowBottom};
        });
    }

    private static void place(View view, Activity activity, int margin, int[] bounds) {
        if (view instanceof AdView && view.getLayoutParams() instanceof FrameLayout.LayoutParams) {
            if (!watched.containsKey(view)) {
                watched.put(view, true);
                // The SDK rebuilds its layout when the window changes. Reapply the navigation reserve afterward.
                view.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, oright, ob) -> refresh(activity));
            }
            FrameLayout.LayoutParams layout = (FrameLayout.LayoutParams) view.getLayoutParams();
            if (layout.bottomMargin != margin) { layout.bottomMargin = margin; view.setLayoutParams(layout); }
            int[] at = new int[2]; view.getLocationOnScreen(at);
            bounds[0] = at[1]; bounds[1] = at[1] + view.getHeight();
            return;
        }
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) place(group.getChildAt(i), activity, margin, bounds);
        }
    }
}
