using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityLib;
using MeshLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;
using Buffer	=SharpDX.Direct3D11.Buffer;
using Device	=SharpDX.Direct3D11.Device;


namespace TestTerrain
{
	internal class DrawRays
	{
		Buffer	mVBRays, mIBRays;

		VertexBufferBinding	mVBBRays;

		int	mRaysIndexCount;

		Vector3	mLightDir;
		Random	mRand	=new Random();

		GraphicsDevice	mGD;

		MatLib	mMatLib;


		internal DrawRays(GraphicsDevice gd, MaterialLib.StuffKeeper sk)
		{
			mGD		=gd;
			mMatLib	=new MatLib(gd, sk);

			mLightDir	=Mathery.RandomDirection(mRand);

			Vector4	lightColor2	=Vector4.One * 0.8f;
			Vector4	lightColor3	=Vector4.One * 0.6f;

			lightColor2.W	=lightColor3.W	=1f;

			mMatLib.CreateMaterial("RayGeometry");
			mMatLib.SetMaterialEffect("RayGeometry", "Static.fx");
			mMatLib.SetMaterialTechnique("RayGeometry", "TriVColorSolidSpec");
			mMatLib.SetMaterialParameter("RayGeometry", "mLightColor0", Vector4.One);
			mMatLib.SetMaterialParameter("RayGeometry", "mLightColor1", lightColor2);
			mMatLib.SetMaterialParameter("RayGeometry", "mLightColor2", lightColor3);
			mMatLib.SetMaterialParameter("RayGeometry", "mSolidColour", Vector4.One);
			mMatLib.SetMaterialParameter("RayGeometry", "mSpecPower", 1);
			mMatLib.SetMaterialParameter("RayGeometry", "mSpecColor", Vector4.One);
			mMatLib.SetMaterialParameter("RayGeometry", "mWorld", Matrix.Identity);
		}


		internal void BuildRayDrawInfo(List<Vector3> rays, float polySize)
		{
			if(mVBRays != null)
			{
				mVBRays.Dispose();
			}
			if(mIBRays != null)
			{
				mIBRays.Dispose();
			}

			if(rays.Count < 2)
			{
				return;
			}

			VPosNormCol0	[]segVerts	=new VPosNormCol0[(rays.Count / 2) * 3];

			UInt32			index		=0;
			List<UInt32>	indexes		=new List<UInt32>();
			for(int i=0;i < rays.Count;i+=2)
			{
				Color	col	=Mathery.RandomColor(mRand);

				col	=Color.Red;

				//endpoint
				segVerts[index].Position	=rays[i + 1];


				Vector3	lineVec	=rays[i + 1] - rays[i];

				//get a perpindicular axis to the a to b axis
				//so the back side of the connection can flare out a bit
				Vector3	crossVec	=Vector3.Cross(lineVec, Vector3.UnitY);

				crossVec.Normalize();

				Vector3	normVec	=Vector3.Cross(crossVec, lineVec);

				normVec.Normalize();

				crossVec	*=2f;

				segVerts[index + 1].Position	=rays[i] - crossVec + Mathery.RandomDirectionXZ(mRand);
				segVerts[index + 2].Position	=rays[i] + crossVec + Mathery.RandomDirectionXZ(mRand);

				//scale up to visible
				segVerts[index].Position.X		*=polySize;
				segVerts[index].Position.Z		*=polySize;
				segVerts[index + 1].Position.X	*=polySize;
				segVerts[index + 1].Position.Z	*=polySize;
				segVerts[index + 2].Position.X	*=polySize;
				segVerts[index + 2].Position.Z	*=polySize;

				segVerts[index].Color0		=col;
				segVerts[index + 1].Color0	=col;
				segVerts[index + 2].Color0	=col;

				Half4	norm;
				norm.X	=normVec.X;
				norm.Y	=normVec.Y;
				norm.Z	=normVec.Z;
				norm.W	=1f;
				segVerts[index].Normal		=norm;
				segVerts[index + 1].Normal	=norm;
				segVerts[index + 2].Normal	=norm;

				indexes.Add(index);
				indexes.Add(index + 1);
				indexes.Add(index + 2);

				index	+=3;
			}

			mVBRays		=VertexTypes.BuildABuffer(mGD.GD, segVerts, VertexTypes.GetIndex(segVerts[0].GetType()));
			mIBRays		=VertexTypes.BuildAnIndexBuffer(mGD.GD, indexes.ToArray());
			mVBBRays	=VertexTypes.BuildAVBB(VertexTypes.GetIndex(segVerts[0].GetType()), mVBRays);

			mRaysIndexCount	=indexes.Count;
		}


		internal void FreeAll()
		{
			mMatLib.FreeAll();

			FreeVBs();
		}


		internal void FreeVBs()
		{
			if(mVBRays != null)
			{
				mVBRays.Dispose();
			}
			if(mIBRays != null)
			{
				mIBRays.Dispose();
			}
		}


		internal void Draw(GameCamera cam)
		{
			if(mVBRays == null)
			{
				return;
			}

			mMatLib.UpdateWVP(Matrix.Identity, cam.View, cam.Projection, cam.Position);

			mMatLib.ApplyMaterialPass("RayGeometry", mGD.DC, 0);

			if(mVBRays != null)
			{
				mGD.DC.InputAssembler.SetVertexBuffers(0, mVBBRays);
				mGD.DC.InputAssembler.SetIndexBuffer(mIBRays, Format.R32_UInt, 0);
				mGD.DC.DrawIndexed(mRaysIndexCount, 0, 0);
			}
		}
	}
}
