package com.pixelgamemaster.emberhall;

import android.app.Activity;
import android.os.Build;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewTreeObserver;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import com.google.android.gms.ads.AdView;
import java.util.ArrayList;

/** Own the safe parent, so SDK Show/resize cannot put a banner over Android navigation. */
public final class EmberhallNavigation {
    private static Controller controller;
    // ready, reserve, navigation, navigationVisible, adTop, adBottom, windowBottom,
    // protectedHosts, visibleFrames, unsafeDraws, guardedLayoutFrames.
    private static volatile int[] metrics = new int[11];

    public static int[] getMetrics() { return metrics.clone(); }

    public static void refresh(Activity activity) {
        activity.runOnUiThread(() -> {
            View root = activity.getWindow().getDecorView();
            if (controller == null || controller.root != root) {
                if (controller != null) controller.dispose();
                controller = new Controller(activity, root);
            }
            controller.update(false);
        });
    }

    /** Explicit QA only: reproduce the fresh LayoutParams assigned by SDK Show/window changes. */
    public static void checkSdkRelayout(Activity activity) {
        activity.runOnUiThread(() -> {
            if (controller == null) return;
            for (BannerHost host : controller.hosts) {
                if (host.getChildCount() == 0) continue;
                host.getChildAt(0).setLayoutParams(new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT,
                    Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL));
            }
        });
    }

    private static final class BannerHost extends FrameLayout {
        BannerHost(Activity activity) {
            super(activity);
            setClipChildren(true); setClipToPadding(true);
            setVisibility(View.INVISIBLE);
        }

        @Override protected void onMeasure(int width, int height) {
            // SDK replacement LayoutParams stay inside this safe host. Reserve navigation
            // on the parent, outside the child's position and cutout adjustments.
            if (getChildCount() > 0) {
                FrameLayout.LayoutParams child = (FrameLayout.LayoutParams) getChildAt(0).getLayoutParams();
                child.gravity = Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL;
                child.setMargins(0, 0, 0, 0);
            }
            super.onMeasure(width, height);
        }
    }

    private static final class Controller implements ViewTreeObserver.OnPreDrawListener, View.OnAttachStateChangeListener {
        final Activity activity;
        final View root;
        final ArrayList<BannerHost> hosts = new ArrayList<>();
        final int[] rootAt = new int[2], parentAt = new int[2], adAt = new int[2];
        int largestNavigation, visibleFrames, unsafeDraws, guardedFrames;

        Controller(Activity activity, View root) {
            this.activity = activity; this.root = root;
            root.getViewTreeObserver().addOnPreDrawListener(this);
            root.addOnAttachStateChangeListener(this);
        }

        void dispose() {
            if (root.getViewTreeObserver().isAlive()) root.getViewTreeObserver().removeOnPreDrawListener(this);
            root.removeOnAttachStateChangeListener(this);
            for (BannerHost host : hosts) host.setVisibility(View.INVISIBLE);
            metrics = new int[11];
        }
        @Override public void onViewAttachedToWindow(View v) { }
        @Override public void onViewDetachedFromWindow(View v) { dispose(); if (controller == this) controller = null; }
        @Override public boolean onPreDraw() { return update(true); }

        int navigation(WindowInsets insets) {
            int nav = insets.getStableInsetBottom();
            if (Build.VERSION.SDK_INT >= 30) nav = Math.max(nav,
                insets.getInsetsIgnoringVisibility(WindowInsets.Type.navigationBars()).bottom);
            if (Build.VERSION.SDK_INT >= 29) nav = Math.max(nav, insets.getMandatorySystemGestureInsets().bottom);
            // Some immersive vendor windows report zero before the first reveal.
            if (nav == 0) {
                int resource = activity.getResources().getIdentifier("navigation_bar_height", "dimen", "android");
                nav = resource == 0 ? Math.round(48 * activity.getResources().getDisplayMetrics().density)
                    : activity.getResources().getDimensionPixelSize(resource);
            }
            largestNavigation = Math.max(largestNavigation, nav);
            return largestNavigation;
        }

        boolean update(boolean beforeDraw) {
            View content = activity.findViewById(android.R.id.content);
            if (content == null) return true;
            boolean changed = mount(content);
            for (int i = hosts.size() - 1; i >= 0; i--) {
                BannerHost host = hosts.get(i);
                if (host.getParent() == null || host.getChildCount() == 0) {
                    if (host.getParent() instanceof ViewGroup) ((ViewGroup) host.getParent()).removeView(host);
                    hosts.remove(i);
                }
            }
            WindowInsets insets = root.getRootWindowInsets();
            if (insets == null || root.getHeight() == 0) {
                for (BannerHost host : hosts) host.setVisibility(View.INVISIBLE);
                metrics = new int[11]; return true;
            }
            int nav = navigation(insets);
            int cutout = Build.VERSION.SDK_INT >= 28 && insets.getDisplayCutout() != null
                ? insets.getDisplayCutout().getSafeInsetBottom() : 0;
            int reserve = Math.max(nav, cutout) + Math.round(8 * activity.getResources().getDisplayMetrics().density);
            root.getLocationOnScreen(rootAt);
            int windowBottom = rootAt[1] + root.getHeight(), top = -1, bottom = -1, ready = 0;
            boolean navVisible = Build.VERSION.SDK_INT >= 30
                ? insets.isVisible(WindowInsets.Type.navigationBars()) : insets.getSystemWindowInsetBottom() > 0;
            for (BannerHost host : hosts) {
                View parent = (View) host.getParent();
                parent.getLocationOnScreen(parentAt);
                int excluded = Math.max(0, windowBottom - parentAt[1] - parent.getHeight());
                FrameLayout.LayoutParams layout = (FrameLayout.LayoutParams) host.getLayoutParams();
                int margin = Math.max(0, reserve - excluded);
                if (layout.bottomMargin != margin) {
                    layout.bottomMargin = margin; host.setLayoutParams(layout); changed = true;
                }
                View ad = host.getChildAt(0); ad.getLocationOnScreen(adAt);
                boolean measured = host.isLaidOut() && !host.isLayoutRequested() && ad.getHeight() > 0;
                boolean safe = measured && adAt[1] + ad.getHeight() <= windowBottom - reserve;
                // Guard EVERY draw, including native bar reveals while Unity is paused.
                // Hide intermediate layouts until Android finishes measuring them.
                host.setVisibility(safe ? View.VISIBLE : View.INVISIBLE);
                if (beforeDraw && !safe && ad.getVisibility() == View.VISIBLE) guardedFrames++;
                if (safe) {
                    top = adAt[1]; bottom = top + ad.getHeight(); ready = 1;
                    if (beforeDraw && !changed && ad.getVisibility() == View.VISIBLE) {
                        visibleFrames++;
                        if (bottom > windowBottom - nav) unsafeDraws++;
                    }
                }
            }
            metrics = new int[] {ready, bottom < 0 ? reserve : windowBottom - bottom, nav,
                navVisible ? 1 : 0, top, bottom, windowBottom, hosts.size(), visibleFrames, unsafeDraws, guardedFrames};
            return !changed;
        }

        boolean mount(View view) {
            if (view instanceof BannerHost) return false;
            if (view instanceof AdView && view.getParent() instanceof FrameLayout) {
                FrameLayout parent = (FrameLayout) view.getParent();
                int index = parent.indexOfChild(view);
                BannerHost host = new BannerHost(activity);
                parent.removeView(view);
                host.addView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL));
                parent.addView(host, index, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM));
                hosts.add(host); return true;
            }
            boolean changed = false;
            if (view instanceof ViewGroup) {
                ViewGroup group = (ViewGroup) view;
                for (int i = 0; i < group.getChildCount(); i++) changed |= mount(group.getChildAt(i));
            }
            return changed;
        }
    }
}
