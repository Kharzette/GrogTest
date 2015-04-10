using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityLib;
using MeshLib;
using PathLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;
using Buffer	=SharpDX.Direct3D11.Buffer;
using Device	=SharpDX.Direct3D11.Device;


namespace TestPathing
{
	internal class DrawPathing
	{
		Buffer	mVBNodes, mIBNodes;
		Buffer	mVBCons, mIBCons;
		Buffer	mVBPath, mIBPath;

		int	mNodeIndexCount;

		VertexBufferBinding	mVBBinding;
		int					mNumIndexes;
		Vector3				mLightDir;
		Random				mRand	=new Random();

		GraphicsDevice	mGD;

		MatLib	mMatLib;


		internal DrawPathing(GraphicsDevice gd, MaterialLib.StuffKeeper sk)
		{
			mGD		=gd;
			mMatLib	=new MatLib(gd, sk);

			mLightDir	=Mathery.RandomDirection(mRand);

			Vector4	lightColor2	=Vector4.One * 0.8f;
			Vector4	lightColor3	=Vector4.One * 0.6f;

			lightColor2.W	=lightColor3.W	=1f;

			mMatLib.CreateMaterial("LevelGeometry");
			mMatLib.SetMaterialEffect("LevelGeometry", "Static.fx");
			mMatLib.SetMaterialTechnique("LevelGeometry", "TriVColorSolidSpec");
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor0", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor1", lightColor2);
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor2", lightColor3);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSolidColour", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSpecPower", 1);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSpecColor", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mWorld", Matrix.Identity);
		}


		internal void BuildDrawInfo(PathGraph graph)
		{
			List<Vector3>	verts		=new List<Vector3>();
			List<Vector3>	norms		=new List<Vector3>();
			List<UInt32>	indexes		=new List<UInt32>();
			List<int>		vertCounts	=new List<int>();
			
			graph.GetNodePolys(verts, indexes, norms, vertCounts);

			VPosNormCol0	[]nodeVerts	=new VPosNormCol0[verts.Count];
			for(int i=0;i < nodeVerts.Length;i++)
			{
				nodeVerts[i].Position	=verts[i];
				nodeVerts[i].Normal.X	=norms[i].X;
				nodeVerts[i].Normal.Y	=norms[i].Y;
				nodeVerts[i].Normal.Z	=norms[i].Z;
				nodeVerts[i].Normal.W	=1f;
			}

			int	idx	=0;
			for(int i=0;i < vertCounts.Count;i++)
			{
				Color	col	=Mathery.RandomColor(mRand);

				for(int j=0;j < vertCounts[i];j++)
				{
					nodeVerts[idx + j].Color0	=col;
				}
				idx	+=vertCounts[i];
			}

			mVBNodes	=VertexTypes.BuildABuffer(mGD.GD, nodeVerts, VertexTypes.GetIndex(nodeVerts[0].GetType()));
			mIBNodes	=VertexTypes.BuildAnIndexBuffer(mGD.GD, indexes.ToArray());
			mVBBinding	=VertexTypes.BuildAVBB(VertexTypes.GetIndex(nodeVerts[0].GetType()), mVBNodes);

			mNodeIndexCount	=indexes.Count;
		}


		internal void FreeAll()
		{
			mMatLib.FreeAll();

			if(mVBNodes != null)
			{
				mVBNodes.Dispose();
			}
			if(mIBNodes != null)
			{
				mIBNodes.Dispose();
			}
			if(mVBCons != null)
			{
				mVBCons.Dispose();
			}
			if(mIBCons != null)
			{
				mIBCons.Dispose();
			}
			if(mVBPath != null)
			{
				mVBPath.Dispose();
			}
			if(mIBPath != null)
			{
				mIBPath.Dispose();
			}
		}


		internal void Draw()
		{
			if(mVBNodes == null)
			{
				return;
			}

			mMatLib.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);

			mMatLib.ApplyMaterialPass("LevelGeometry", mGD.DC, 0);

			mGD.DC.InputAssembler.SetVertexBuffers(0, mVBBinding);
			mGD.DC.InputAssembler.SetIndexBuffer(mIBNodes, Format.R32_UInt, 0);

			mGD.DC.DrawIndexed(mNodeIndexCount, 0, 0);
		}
	}
}
