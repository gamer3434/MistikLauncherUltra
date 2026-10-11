package tr.mistik.hud;

final class ClickCounter {
    private final long[] clicks = new long[128];
    private int cursor;
    void record(long now) { clicks[cursor++ & 127] = now; }
    int count(long now) {
        int result = 0;
        for (long click : clicks) if (click != 0 && click > now - 1000 && click <= now) result++;
        return result;
    }
}
