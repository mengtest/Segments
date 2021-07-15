﻿using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Assertions;

using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace Segments.Samples
{
	[ExecuteAlways]
	[AddComponentMenu("")]
	class StressTests : MonoBehaviour
	{
		
		[SerializeField] Material _srcMaterial = null;
		[SerializeField] int _numSegments = 128;
		[SerializeField] float _frequency = 16;
		[SerializeField] bool _everyFrame = false;

		Segments.SegmentRenderingSystem _segmentsSystem;
		Segments.Batch _segments;
		

		void OnEnable ()
		{
			_segmentsSystem = Segments.Core.GetRenderingSystem();
			_segmentsSystem.CreateBatch( out _segments , _srcMaterial );
		}


		void OnDisable ()
		{
			if( _segments!=null )
			{
				_segments.Dependency.Complete();
				_segments.Dispose();
				_segments = null;
			}
		}


		void Update ()
		{
			// complete previous job:
			_segments.Dependency.Complete();

			if( _segments.buffer.Length!=_numSegments || _everyFrame )
			{
				// set buffer length:
				_segments.buffer.Length = _numSegments;

				// scheduel new job:
				var job = new MyJob{
					transform		= transform.localToWorldMatrix ,
					numSegments		= _numSegments ,
					segments		= _segments.buffer.AsArray().Slice() ,
					offset			= Time.time ,
					frequency		= _frequency ,
				};
				_segments.Dependency = job.Schedule(
					arrayLength:			_segments.buffer.Length ,
					innerloopBatchCount:	64 ,
					dependsOn:				_segments.Dependency
				);
			}
		}


		[BurstCompile]
		public struct MyJob : IJobParallelFor
		{
			public float4x4 transform;
			public int numSegments;
			public float offset;
			public float frequency;
			[WriteOnly] public NativeSlice<float3x2> segments;
			void IJobParallelFor.Execute ( int index )
			{
				float t0 = (float) index / (float) numSegments;
				float t1 = (float)( index+1 ) / (float) numSegments;
				float amp0 = math.sin( frequency * ( t0*math.PI*2f + offset ) );
				float amp1 = math.sin( frequency * ( t1*math.PI*2f + offset ) );
				float3 vec0 = math.transform( transform , new float3{ x=t0 , y=amp0 } );
				float3 vec1 = math.transform( transform , new float3{ x=t1 , y=amp1 } );
				segments[index] = new float3x2{ c0=vec0 , c1=vec1 };
			}
		}
		

		[UnityEditor.CustomEditor( typeof(StressTests) )]
		public class MyEditor : UnityEditor.Editor
		{
			public override void OnInspectorGUI ()
			{
				DrawDefaultInspector();
				if( GUILayout.Button("Update batch") )
				{
					var instance = (StressTests) target;
					instance.OnDisable();
					instance.OnEnable();
				}
			}

			public override VisualElement CreateInspectorGUI ()
			{
				var ROOT = new VisualElement();
				Rebind(ROOT);
				return ROOT;
			}

			void Rebind ( VisualElement ROOT )
			{
				var instance = (StressTests) target;
				System.Action rebuild = ()=>{
					instance.OnDisable();
					instance.OnEnable();
				};

				var MATERIAL = new UnityEditor.UIElements.ObjectField("Material");
				MATERIAL.objectType = typeof(Material);
				MATERIAL.value = instance._srcMaterial;
				MATERIAL.RegisterValueChangedCallback( (ctx) => {
					instance._srcMaterial = (Material) ctx.newValue;
				} );
				ROOT.Add( MATERIAL );

				var NUM = new UnityEditor.UIElements.IntegerField("Num Segments");
				NUM.value = instance._numSegments;
				NUM.RegisterValueChangedCallback( (ctx) => {
					instance._numSegments = ctx.newValue;

					// const int max = 100_000;
					// if( instance._numSegments > max )
					// {
					// 	instance._numSegments = max;
					// 	NUM.SetValueWithoutNotify( max );
					// }

					const int min = 1;
					if( instance._numSegments < min )
					{
						instance._numSegments = min;
						NUM.SetValueWithoutNotify( min );
					}
					
					// rebuild();
					// ROOT.Clear();
					// Rebind(ROOT);
				} );
				ROOT.Add( NUM );

				var FREQ = new UnityEditor.UIElements.FloatField("Frequency");
				FREQ.value = instance._frequency;
				FREQ.RegisterValueChangedCallback( (ctx) => {
					instance._frequency = ctx.newValue;
				} );
				ROOT.Add( FREQ );

				var LOOP = new Toggle("Every Frame");
				LOOP.value = instance._everyFrame;
				LOOP.RegisterValueChangedCallback( (ctx) => {
					instance._everyFrame = ctx.newValue;
				} );
				ROOT.Add( LOOP );
				
				var BUTTON = new Button( ()=>{
					rebuild();
					ROOT.Clear();
					Rebind(ROOT);
				} );
				BUTTON.text = "Update batch";
				ROOT.Add( BUTTON );

				if( instance._segments!=null )
				{
					var LABEL = new Label("Batch data:");
					LABEL.style.marginTop = LABEL.style.marginTop.value.value + 12f;
					ROOT.Add( LABEL );

					var MATERIAL_INSTANCE = new UnityEditor.UIElements.ObjectField("Material instance (copy)");
					MATERIAL_INSTANCE.objectType = typeof(Material);
					MATERIAL_INSTANCE.value = instance._segments.material;
					ROOT.Add( MATERIAL_INSTANCE );

					var MESH_INSTANCE = new UnityEditor.UIElements.ObjectField("Mesh instance");
					MESH_INSTANCE.objectType = typeof(Mesh);
					MESH_INSTANCE.value = instance._segments.mesh;
					ROOT.Add( MESH_INSTANCE );
				}
				else
				{
					var LABEL = new Label("( no batch data )");
					LABEL.style.marginTop = LABEL.style.marginTop.value.value + 12f;
					ROOT.Add( LABEL );
				}
			}
			
		}

		
	}
}
