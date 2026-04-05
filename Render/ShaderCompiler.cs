using System;
using System.Runtime.InteropServices;
using Silk.NET.Shaderc;

namespace PlanetaryTerrainRenderer.Render
{
    public unsafe class ShaderCompiler : IDisposable
    {
        private Shaderc _shaderc;
        private Silk.NET.Shaderc.Compiler* _compiler;
        private CompileOptions* _options;

        public ShaderCompiler()
        {
            _shaderc = Shaderc.GetApi();
            _compiler = _shaderc.CompilerInitialize();
            _options = _shaderc.CompileOptionsInitialize();
            _shaderc.CompileOptionsSetSourceLanguage(_options, SourceLanguage.Hlsl);
            _shaderc.CompileOptionsSetOptimizationLevel(_options, OptimizationLevel.Performance);
        }

        public byte[] CompileHLSL(string source, string name, ShaderKind kind)
        {
            var result = _shaderc.CompileIntoSpv(_compiler, source, (nuint)source.Length, kind, name, "main", _options);
            
            if (_shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
            {
                var errPtr = _shaderc.ResultGetErrorMessage(result);
                string err = Marshal.PtrToStringAnsi((nint)errPtr) ?? "Unknown Shader Compiler Error";
                _shaderc.ResultRelease(result);
                throw new Exception($"Shader compilation failed for {name}: {err}");
            }

            nuint length = _shaderc.ResultGetLength(result);
            byte* bytes = _shaderc.ResultGetBytes(result);

            byte[] bytecode = new byte[length];
            Marshal.Copy((nint)bytes, bytecode, 0, (int)length);

            _shaderc.ResultRelease(result);
            return bytecode;
        }

        public void Dispose()
        {
            _shaderc.CompileOptionsRelease(_options);
            _shaderc.CompilerRelease(_compiler);
            _shaderc.Dispose();
        }
    }
}
