﻿using Apos.Camera;
using Apos.Input;
using Apos.Shapes;
using Apos.Spatial;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

// TODO: Redo
//       Save
// FIXME: Undo

namespace GameProject {
    public class GameRoot : Game {
        public GameRoot() {
            _graphics = new GraphicsDeviceManager(this);
            IsMouseVisible = true;
            Content.RootDirectory = "Content";
        }

        protected override void Initialize() {
            Window.AllowUserResizing = true;

            base.Initialize();
        }

        protected override void LoadContent() {
            _s = new SpriteBatch(GraphicsDevice);
            _sb = new ShapeBatch(GraphicsDevice, Content);

            // TODO: use this.Content to load your game content here
            InputHelper.Setup(this);

            _fontSystem = new FontSystem();
            _fontSystem.AddFont(TitleContainer.OpenStream($"{Content.RootDirectory}/source-code-pro-medium.ttf"));

            _lines = new Dictionary<int, Line>();
            _tree = new AABBTree<Line>();

            _camera = new Camera(new DefaultViewport(GraphicsDevice, Window));
        }

        protected override void Update(GameTime gameTime) {
            InputHelper.UpdateSetup();

            if (_quit.Pressed())
                Exit();

            if (_resetFPS.Pressed()) _fps.DroppedFrames = 0;
            _fps.Update(gameTime);

            UpdateCamera();

            if (_undo.Pressed() && _lines.Count > 0) {
                _nextId--;
                Line l = _lines[_nextId];
                _lines.Remove(_nextId);
                _tree.Remove(l.Leaf);
            }

            if (_draw.Pressed()) {
                _start = _mouseWorld;
                _isDrawing = true;
            }
            if (_isDrawing && _draw.Held()) {
                _end = _mouseWorld;

                if (_start != _end && !_line.Held()) {
                    CreateLine(_nextId++, _start, _end, _radius * _camera.ScreenToWorldScale());
                    _start = _mouseWorld;
                }
            }
            if (_isDrawing && _draw.Released()) {
                _isDrawing = false;
                _end = _mouseWorld;

                CreateLine(_nextId++, _start, _end, _radius * _camera.ScreenToWorldScale());
            }

            InputHelper.UpdateCleanup();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            _fps.Draw(gameTime);
            GraphicsDevice.Clear(TWColor.Black);

            _sb.Begin(_camera.View);
            int inView = 0;
            foreach (Line l in _tree.Query(_camera.ViewRect).OrderBy(e => e.Id)) {
                _sb.FillLine(l.A, l.B, l.Radius, TWColor.Gray300);
                inView++;
            }
            if (_isDrawing) {
                _sb.FillLine(_start, _end, _radius * _camera.ScreenToWorldScale(), TWColor.Gray300);
            }
            _sb.End();

            var font = _fontSystem.GetFont(24);
            _s.Begin();
            _s.DrawString(font, $"fps: {_fps.FramesPerSecond} - Dropped Frames: {_fps.DroppedFrames} - Draw ms: {_fps.TimePerFrame} - Update ms: {_fps.TimePerUpdate}", new Vector2(10, 10), TWColor.White);
            _s.DrawString(font, $"In view: {inView} -- Total: {_lines.Count} -- {_camera.ScreenToWorldScale()}", new Vector2(10, GraphicsDevice.Viewport.Height - 24), TWColor.White);
            _s.End();

            base.Draw(gameTime);
        }

        public void UpdateCamera() {
            if (MouseCondition.Scrolled()) {
                _targetExp = MathHelper.Clamp(_targetExp - MouseCondition.ScrollDelta * _expDistance, _maxExp, _minExp);
            }

            if (_rotateLeft.Pressed()) {
                _targetRotation += MathHelper.PiOver4;
            }
            if (_rotateRight.Pressed()) {
                _targetRotation -= MathHelper.PiOver4;
            }

            _mouseWorld = _camera.ScreenToWorld(InputHelper.NewMouse.X, InputHelper.NewMouse.Y);

            if (_dragCamera.Pressed()) {
                _dragAnchor = _mouseWorld;
                _isDragging = true;
            }
            if (_isDragging && _dragCamera.HeldOnly()) {
                _camera.XY += _dragAnchor - _mouseWorld;
                _mouseWorld = _dragAnchor;
            }
            if (_isDragging && _dragCamera.Released()) {
                _isDragging = false;
            }

            _camera.Z = _camera.ScaleToZ(ExpToScale(Interpolate(ScaleToExp(_camera.ZToScale(_camera.Z, 0f)), _targetExp, _speed, _snapDistance)), 0f);
            _camera.Rotation = Interpolate(_camera.Rotation, _targetRotation, _speed, _snapDistance);
        }
        private float Interpolate(float from, float target, float speed, float snapNear) {
            float result = MathHelper.Lerp(from, target, speed);

            if (from < target) {
                result = MathHelper.Clamp(result, from, target);
            } else {
                result = MathHelper.Clamp(result, target, from);
            }

            if (MathF.Abs(target - result) < snapNear) {
                return target;
            } else {
                return result;
            }
        }
        private float ScaleToExp(float scale) {
            return -MathF.Log(scale);
        }
        private float ExpToScale(float exp) {
            return MathF.Exp(-exp);
        }

        private void CreateLine(int id, Vector2 a, Vector2 b, float radius) {
            Line l = new Line(id, a, b, radius);

            l.Leaf = _tree.Add(l.AABB, l);
            _lines.Add(id, l);
        }

        private class Line {
            public Line(int id, Vector2 a, Vector2 b, float radius) {
                Id = id;
                A = a;
                B = b;
                Radius = radius;
                AABB = ComputeAABB();
            }

            public int Id { get; set; }
            public int Leaf { get; set; }
            public Vector2 A { get; set; }
            public Vector2 B { get; set; }
            public float Radius { get; set; }

            public RectangleF AABB { get; set; }

            private RectangleF ComputeAABB() {
                float left = MathF.Min(A.X, B.X) - Radius;
                float top = MathF.Min(A.Y, B.Y) - Radius;
                float right = MathF.Max(A.X, B.X) + Radius;
                float bottom = MathF.Max(A.Y, B.Y) + Radius;

                return new RectangleF(left, top, right - left, bottom - top);
            }
        }

        GraphicsDeviceManager _graphics;
        Camera _camera;
        SpriteBatch _s;
        ShapeBatch _sb;
        FontSystem _fontSystem;

        AABBTree<Line> _tree;
        Dictionary<int, Line> _lines;

        int _nextId;

        ICondition _quit =
            new AnyCondition(
                new KeyboardCondition(Keys.Escape),
                new GamePadCondition(GamePadButton.Back, 0)
            );

        ICondition _draw = new MouseCondition(MouseButton.LeftButton);
        ICondition _line =
                new AnyCondition(
                    new KeyboardCondition(Keys.LeftShift),
                    new KeyboardCondition(Keys.RightShift)
                );
        ICondition _rotateLeft = new KeyboardCondition(Keys.OemComma);
        ICondition _rotateRight = new KeyboardCondition(Keys.OemPeriod);

        ICondition _dragCamera = new MouseCondition(MouseButton.MiddleButton);

        ICondition _resetFPS = new KeyboardCondition(Keys.F2);

        ICondition _undo =
            new AllCondition(
                new AnyCondition(
                    new KeyboardCondition(Keys.LeftControl),
                    new KeyboardCondition(Keys.RightControl)
                ),
                new KeyboardCondition(Keys.Z)
            );
        ICondition _redo =
            new AllCondition(
                new AnyCondition(
                    new KeyboardCondition(Keys.LeftControl),
                    new KeyboardCondition(Keys.RightControl)
                ),
                new AnyCondition(
                    new KeyboardCondition(Keys.LeftShift),
                    new KeyboardCondition(Keys.RightShift)
                ),
                new KeyboardCondition(Keys.Z)
            );

        bool _isDrawing = false;
        Vector2 _start;
        Vector2 _end;
        float _radius = 10f;

        Vector2 _mouseWorld;
        Vector2 _dragAnchor = Vector2.Zero;
        bool _isDragging = false;
        float _targetExp = 0f;
        float _targetRotation = 0f;
        float _speed = 0.08f;
        float _snapDistance = 0.001f;
        float _expDistance = 0.002f;
        float _maxExp = -4f;
        float _minExp = 4f;

        FPSCounter _fps = new FPSCounter();
    }
}
