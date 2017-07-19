﻿using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OBJExporterUI.Loaders;
using System.Drawing;
using OpenTK.Input;
using System.IO;

namespace OBJExporterUI
{
    public class PreviewControl
    {
        private GLControl renderCanvas;

        private bool ready = false;
        private string modelType;

        // Cache storage for models... bad idea?
        private CacheStorage cache = new CacheStorage();

        private NewCamera ActiveCamera;

        private string filename;

        private int adtShaderProgram;
        private int wmoShaderProgram;
        private int m2ShaderProgram;

        public PreviewControl(GLControl renderCanvas)
        {
            this.renderCanvas = renderCanvas;
            this.renderCanvas.Paint += RenderCanvas_Paint;
            this.renderCanvas.Load += RenderCanvas_Load;
            this.renderCanvas.Resize += RenderCanvas_Resize;

            ActiveCamera = new NewCamera(renderCanvas.Width, renderCanvas.Height, new Vector3(0, 0, -1), new Vector3(-11, 0, 0), Vector3.UnitZ);
        }

        private void RenderCanvas_Resize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            if(renderCanvas.Width > 0 && renderCanvas.Height > 0)
            {
                ActiveCamera.viewportSize(renderCanvas.Width, renderCanvas.Height);
            }
        }

        public void LoadModel(string filename)
        {
            ready = false;

            GL.ActiveTexture(TextureUnit.Texture0);

            this.filename = filename;

            if (filename.EndsWith(".m2"))
            {
                if (!cache.doodadBatches.ContainsKey(filename))
                {
                    M2Loader.LoadM2(filename, cache, m2ShaderProgram);
                }
                modelType = "m2";
                ActiveCamera.Pos = new Vector3((cache.doodadBatches[filename].boundingBox.max.Z) + 11.0f, 0.0f, 4.0f);
            }
            else if (filename.EndsWith(".wmo"))
            {
                if (!cache.worldModels.ContainsKey(filename))
                {
                    WMOLoader.LoadWMO(filename, cache, wmoShaderProgram);
                }
                modelType = "wmo";
            }else if (filename.EndsWith(".adt"))
            {
                if (!cache.terrain.ContainsKey(filename))
                {
                    ADTLoader.LoadADT(filename, cache, adtShaderProgram);
                }

                ActiveCamera.Pos = new Vector3(cache.terrain[filename].startPos.Position.X, cache.terrain[filename].startPos.Position.Y, cache.terrain[filename].startPos.Position.Z);
                modelType = "adt";
            }

            ready = true;
        }

        public void WindowsFormsHost_Initialized(object sender, EventArgs e)
        {
            renderCanvas.MakeCurrent();
        }

        private void Update()
        {
            if (!renderCanvas.Focused) return;

            MouseState mouseState = Mouse.GetState();
            KeyboardState keyboardState = Keyboard.GetState();

            ActiveCamera.processKeyboardInput(keyboardState);

            return;
        }

        private void RenderCanvas_Load(object sender, EventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);

            adtShaderProgram = Shader.CompileShader("adt");
            wmoShaderProgram = Shader.CompileShader("wmo");
            m2ShaderProgram = Shader.CompileShader("m2");

            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        private void RenderCanvas_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (!ready) return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            GL.Enable(EnableCap.Texture2D);

            if (modelType == "m2")
            {
                GL.UseProgram(m2ShaderProgram);

                ActiveCamera.setupGLRenderMatrix(m2ShaderProgram);
                ActiveCamera.flyMode = false;

                GL.BindVertexArray(cache.doodadBatches[filename].vao);

                for (int i = 0; i < cache.doodadBatches[filename].submeshes.Length; i++)
                {
                    GL.BindTexture(TextureTarget.Texture2D, cache.doodadBatches[filename].submeshes[i].material);
                    GL.DrawRangeElements(PrimitiveType.Triangles, cache.doodadBatches[filename].submeshes[i].firstFace, (cache.doodadBatches[filename].submeshes[i].firstFace + cache.doodadBatches[filename].submeshes[i].numFaces), (int)cache.doodadBatches[filename].submeshes[i].numFaces, DrawElementsType.UnsignedInt, new IntPtr(cache.doodadBatches[filename].submeshes[i].firstFace * 4));
                }
            }
            else if (modelType == "wmo")
            {
                GL.UseProgram(wmoShaderProgram);

                ActiveCamera.setupGLRenderMatrix(wmoShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(wmoShaderProgram, "alphaRef");

                for (int j = 0; j < cache.worldModelBatches[filename].wmoRenderBatch.Length; j++)
                {
                    GL.BindVertexArray(cache.worldModelBatches[filename].groupBatches[cache.worldModelBatches[filename].wmoRenderBatch[j].groupID].vao);

                    switch(cache.worldModelBatches[filename].wmoRenderBatch[j].blendType)
                    {
                        case 0:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                        case 1:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, 0.90393700787f);
                            break;
                        case 2:
                            GL.Enable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                            break;
                        default:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                    }

                    GL.BindTexture(TextureTarget.Texture2D, cache.worldModelBatches[filename].wmoRenderBatch[j].materialID[0]);
                    GL.DrawElements(PrimitiveType.Triangles, (int)cache.worldModelBatches[filename].wmoRenderBatch[j].numFaces, DrawElementsType.UnsignedInt, (int)cache.worldModelBatches[filename].wmoRenderBatch[j].firstFace * 4);
                }
            }else if(modelType == "adt")
            {
                GL.UseProgram(adtShaderProgram);

                ActiveCamera.setupGLRenderMatrix(adtShaderProgram);
                ActiveCamera.flyMode = true;

                GL.BindVertexArray(cache.terrain[filename].vao);

                for (int i = 0; i < cache.terrain[filename].renderBatches.Length; i++)
                {
                    for (int j = 0; j < cache.terrain[filename].renderBatches[i].materialID.Length; j++)
                    {
                        var textureLoc = GL.GetUniformLocation(adtShaderProgram, "layer" + j);
                        GL.Uniform1(textureLoc, j);

                        GL.ActiveTexture(TextureUnit.Texture0 + j);
                        GL.BindTexture(TextureTarget.Texture2D, (int)cache.terrain[filename].renderBatches[i].materialID[j]);
                    }

                    for (int j = 1; j < cache.terrain[filename].renderBatches[i].alphaMaterialID.Length; j++)
                    {
                        var textureLoc = GL.GetUniformLocation(adtShaderProgram, "alphaLayer" + j);
                        GL.Uniform1(textureLoc, 3 + j);

                        GL.ActiveTexture(TextureUnit.Texture3 + j);
                        GL.BindTexture(TextureTarget.Texture2D, cache.terrain[filename].renderBatches[i].alphaMaterialID[j]);
                    }

                    GL.DrawRangeElements(PrimitiveType.Triangles, (int)cache.terrain[filename].renderBatches[i].firstFace, (int)cache.terrain[filename].renderBatches[i].firstFace + (int)cache.terrain[filename].renderBatches[i].numFaces, (int)cache.terrain[filename].renderBatches[i].numFaces, DrawElementsType.UnsignedInt, new IntPtr(cache.terrain[filename].renderBatches[i].firstFace * 4));
                }
            }

            var error = GL.GetError().ToString();

            if (error != "NoError")
            {
                throw new Exception(error);
            }

            GL.BindVertexArray(0);
            renderCanvas.SwapBuffers();
        }

        public void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            Update();
            renderCanvas.Invalidate();
        }
    }
}
