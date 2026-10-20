using AshenReliquary.Core.Content.Flare;

namespace AshenReliquary.Core.Tests;

public class FlareFormatTests
{
    private const string Tileset = """
        [tileset]
        img=images/tilesets/a.png
        tile=16,10,20,192,96,96,48
        tile=17,202,20,192,96,96,48
        # comment
        img=images/tilesets/water.png
        tile=176,0,0,192,96,96,48
        animation=176;0,0,250ms;192,0,250ms;
        """;

    private const string Animation = """
        image=images/enemies/zombie.png

        [stance]
        frames=2
        duration=533ms
        type=back_forth
        frame=0,0,1,2,30,40,15,38
        frame=1,0,31,2,30,40,15,38
        frame=0,6,61,2,30,40,14,37
        frame=1,6,91,2,30,40,14,37

        [swing]
        frames=4
        duration=400ms
        type=play_once
        active_frame=2
        frame=0,0,0,0,10,10,5,9
        """;

    [Fact]
    public void Tileset_tiles_belong_to_the_image_above_them()
    {
        var set = FlareTileset.Parse(Tileset);
        Assert.Equal(3, set.Tiles.Count);
        Assert.Equal(new FlareTile(16, "images/tilesets/a.png", 10, 20, 192, 96, 96, 48), set.Tiles[16] with { Animation = [] });
        Assert.Equal("images/tilesets/water.png", set.Tiles[176].Image);
        Assert.Equal([(0, 0, 250), (192, 0, 250)], set.Tiles[176].Animation);
        Assert.Equal(["images/tilesets/a.png", "images/tilesets/water.png"], set.Images);
    }

    [Fact]
    public void Animation_sets_read_frames_by_index_and_direction()
    {
        var set = FlareAnimationSet.Parse(Animation);
        Assert.Equal("images/enemies/zombie.png", set.Image);
        var stance = set["stance"]!;
        Assert.Equal((2, 533, FlareLoop.BackForth), (stance.FrameCount, stance.DurationMs, stance.Loop));
        Assert.Equal(new FlareFrame(91, 2, 30, 40, 14, 37), stance.Frame(1, 6));
        Assert.Equal(2, set["swing"]!.ActiveFrame);
        Assert.Null(set["fly"]);
    }

    [Fact]
    public void The_older_grid_layout_gives_each_frame_a_cell_one_row_per_direction()
    {
        var set = FlareAnimationSet.Parse("""
            image=images/enemies/wolf.png

            render_size=128,128
            render_offset=64,96

            [stance]
            position=0
            frames=4
            duration=800ms
            type=back_forth

            [run]
            position=4
            frames=8
            duration=533ms
            type=looped
            """);
        Assert.Equal(new FlareFrame(0, 0, 128, 128, 64, 96), set["stance"]!.Frame(0, 0));
        Assert.Equal(new FlareFrame((4 + 2) * 128, 3 * 128, 128, 128, 64, 96), set["run"]!.Frame(2, 3));
        Assert.Equal(FlareLoop.BackForth, set["stance"]!.Loop);
    }

    [Fact]
    public void Keyed_sheets_give_each_animation_the_sheet_its_frames_name()
    {
        var set = FlareAnimationSet.Parse("""
            image=images/enemies/minotaur/minotaur_stance.png,stance
            image=images/enemies/minotaur/minotaur_swing.png,swing

            [stance]
            frames=1
            duration=1600ms
            type=looped
            frame=0,0,0,1092,157,248,76,227,stance

            [run]
            frames=1
            duration=500ms
            frame=0,2,10,20,30,40,5,35,swing
            """);
        Assert.Equal("images/enemies/minotaur/minotaur_stance.png", set.Image);
        Assert.Equal("images/enemies/minotaur/minotaur_stance.png", set["stance"]!.Image);
        Assert.Equal(new FlareFrame(0, 1092, 157, 248, 76, 227), set["stance"]!.Frame(0, 0));
        Assert.Equal("images/enemies/minotaur/minotaur_swing.png", set["run"]!.Image);   // by the frames' key, not the name
    }

    [Fact]
    public void Frame_timing_follows_the_loop_type()
    {
        var swing = FlareAnimationSet.Parse(Animation)["swing"]!; // 4 frames over 0.4 s, once
        Assert.Equal(0, swing.FrameAt(0.0));
        Assert.Equal(2, swing.FrameAt(0.21));
        Assert.Equal(3, swing.FrameAt(5.0));
    }

    [Fact]
    public void Shipped_flare_files_parse()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Flare");
        var zombie = FlareAnimationSet.Parse(File.ReadAllText(Path.Combine(root, "animations/enemies/zombie.txt")));
        Assert.Contains("critdie", zombie.Animations.Keys);
        Assert.All(Enumerable.Range(0, 8), d => Assert.True(zombie["run"]!.Frame(0, d).Width > 0));
        var wolf = FlareAnimationSet.Parse(File.ReadAllText(Path.Combine(root, "animations/enemies/wolf.txt")));
        Assert.Equal(["stance", "run", "swing", "hit", "die"], new[] { "stance", "run", "swing", "hit", "die" }.Where(wolf.Animations.ContainsKey));
        // Re-rendered at 256-px frames and packed: every frame of every direction is there.
        Assert.All(wolf.Animations.Values, a => Assert.All(Enumerable.Range(0, a.FrameCount),
            f => Assert.All(Enumerable.Range(0, 8), d => Assert.True(a.Frame(f, d).Width > 0, $"{a.Name} {f} {d}"))));
        var arrows = FlareAnimationSet.Parse(File.ReadAllText(Path.Combine(root, "animations/powers/arrows.txt")));
        Assert.All(Enumerable.Range(0, 8), d => Assert.True(arrows.Animations.Values.First().Frame(0, d).Width > 0));
        var grass = FlareTileset.Parse(File.ReadAllText(Path.Combine(root, "tilesetdefs/tileset_grassland.txt")));
        Assert.True(grass.Tiles.Count > 200);
        Assert.Contains(265, grass.Tiles.Keys);
    }
}
